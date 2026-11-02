using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RespondaRapido.Domain;

namespace RespondaRapido.Infrastructure;

public sealed class GameSession(IQuestionCatalog catalog, IPrizeLadder ladder, IScoringPolicy scoring, IAnswerWindow answerWindow,
    IQuestionPicker picker, IOptions<QuizSettings> settings, ILogger<GameSession> logger)
{
    private readonly object gate = new();
    private readonly ConcurrentDictionary<WebSocket, SemaphoreSlim> sendLocks = new();
    private readonly Dictionary<string, (Player Player, WebSocket Socket)> players = [];
    private readonly HashSet<int> usedQuestions = [];
    private readonly JsonSerializerOptions jsonOptions = CreateJsonOptions();
    private WebSocket? host;
    private string[] selectedSubjects = [];
    private QuizQuestion? current;
    private int questionIndex = -1;
    private DateTimeOffset? deadline;
    private GamePhase phase = GamePhase.Lobby;
    private string? notice;


    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var first = await ReceiveAsync(socket, cancellationToken);
        if (first is null) return;
        var kind = ReadString(first.Value, "type");
        if (kind == "host") await ConnectHostAsync(socket, cancellationToken);
        else if (kind == "join") await JoinPlayerAsync(socket, ReadString(first.Value, "name"), cancellationToken);
        else await CloseAsync(socket);
    }

    private async Task ConnectHostAsync(WebSocket socket, CancellationToken ct)
    {
        var hostTaken = false;
        lock (gate) { hostTaken = host is not null; if (!hostTaken) host = socket; }
        if (hostTaken) { await SendMessageAsync(socket, new { type = "rejected", message = "A tela do anfitrião já está conectada." }, ct); await CloseAsync(socket); return; }
        await SendViewAsync(socket, "host", ct);
        await BroadcastAsync();
        await ReadCommandsAsync(socket, "host", null, ct);
    }

    private async Task JoinPlayerAsync(WebSocket socket, string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        var min = Math.Clamp(settings.Value.LobbyNameMinLength, 1, 40);
        var max = Math.Clamp(settings.Value.LobbyNameMaxLength, min, 40);
        string? error = null;
        lock (gate) {
            if (host is null) error = "A tela do anfitrião ainda não iniciou a partida.";
            else if (phase != GamePhase.Lobby) error = "A partida já começou.";
            else if (name.Length < min || name.Length > max || name.Any(char.IsControl)) error = $"Use um nome entre {min} e {max} caracteres.";
            else if (players.Values.Any(p => string.Equals(p.Player.Name, name, StringComparison.OrdinalIgnoreCase))) error = "Esse nome já está em uso.";
            else { var id = Guid.NewGuid().ToString("N"); players[id] = (new Player(id, name, 0, false, null, null), socket); }
        }
        if (error is not null) { await SendMessageAsync(socket, new { type = "rejected", message = error }, ct); await CloseAsync(socket); return; }
        await SendViewAsync(socket, "player", ct); await BroadcastAsync();
        await ReadCommandsAsync(socket, "player", FindPlayerId(socket), ct);
    }

    private async Task ReadCommandsAsync(WebSocket socket, string role, string? playerId, CancellationToken ct)
    {
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            var message = await ReceiveAsync(socket, ct);
            if (message is null) break;
            try {
                var type = ReadString(message.Value, "type");
                string? error = null;
                lock (gate) {
                    if (role == "host" && ReferenceEquals(host, socket)) {
                        if (type == "start") error = StartGame(ReadStringArray(message.Value, "subjects"));
                        else if (type == "next") error = Advance();
                    } else if (role == "player" && playerId is not null && type == "answer") error = AcceptAnswer(playerId, ReadInt(message.Value, "option"));
                }
                if (error is not null) await SendMessageAsync(socket, new { type = "notice", message = error }, ct);
                await BroadcastAsync();
            } catch (JsonException) { await SendMessageAsync(socket, new { type = "notice", message = "Mensagem inválida." }, ct); }
        }
        await DisconnectAsync(socket, role, playerId);
    }

    private string? StartGame(string[] subjects)
    {
        if (phase != GamePhase.Lobby) return "A partida já começou.";
        if (players.Count == 0) return "É preciso ter ao menos um participante.";
        var valid = catalog.Subjects.Select(s => s.Code).ToHashSet();
        selectedSubjects = subjects.Where(valid.Contains).Distinct().ToArray();
        if (selectedSubjects.Length == 0) return "Escolha pelo menos uma matéria.";
        foreach (var group in Enumerable.Range(0, ladder.TotalQuestions).GroupBy(ladder.GetDifficulty)) {
            var needed = group.Count();
            var available = catalog.Questions.Count(q => q.Difficulty == group.Key && selectedSubjects.Contains(q.SubjectCode));
            if (available < needed) return $"As matérias selecionadas têm {available} perguntas de nível {group.Key}, mas a partida precisa de {needed}.";
        }
        questionIndex = 0;
        return OpenQuestion();
    }

    private string? Advance()
    {
        if (phase != GamePhase.QuestionClosed) return "A pergunta atual ainda está aberta.";
        if (questionIndex + 1 >= ladder.TotalQuestions) { phase = GamePhase.Finished; current = null; notice = null; return null; }
        questionIndex++;
        return OpenQuestion();
    }

    private string? OpenQuestion()
    {
        try { current = picker.Pick(catalog.Questions, ladder.GetDifficulty(questionIndex), selectedSubjects.ToHashSet(), usedQuestions); }
        catch (InvalidOperationException ex) { return ex.Message; }
        usedQuestions.Add(current.Id);
        deadline = answerWindow.Open(DateTimeOffset.UtcNow);
        phase = GamePhase.QuestionOpen; notice = null;
        foreach (var (id, pair) in players.ToArray()) players[id] = (pair.Player with { Answered = false, Correct = null, SelectedOption = null }, pair.Socket);
        return null;
    }

    private string? AcceptAnswer(string id, int option)
    {
        if (phase != GamePhase.QuestionOpen || current is null || deadline is null || answerWindow.IsExpired(deadline.Value, DateTimeOffset.UtcNow)) return "O tempo para responder acabou.";
        if (option is < 0 or > 3) return "Alternativa inválida.";
        if (!players.TryGetValue(id, out var entry)) return "Participante não encontrado.";
        if (entry.Player.Answered) return "Sua resposta já foi registrada.";
        players[id] = (entry.Player with { Answered = true, Correct = option == current.CorrectOption, SelectedOption = option }, entry.Socket);
        return null;
    }

    public async Task TickAsync(CancellationToken ct)
    {
        var changed = false;
        lock (gate) {
            if (phase == GamePhase.QuestionOpen && deadline is not null && answerWindow.IsExpired(deadline.Value, DateTimeOffset.UtcNow)) {
                foreach (var (id, pair) in players.ToArray()) {
                    var correct = pair.Player.Correct == true;
                    players[id] = (pair.Player with { Balance = scoring.Apply(pair.Player.Balance, correct, ladder.GetPrize(questionIndex)) }, pair.Socket);
                }
                phase = GamePhase.QuestionClosed; changed = true;
            }
        }
        if (changed) await BroadcastAsync(ct);
    }

    private async Task SendViewAsync(WebSocket socket, string role, CancellationToken ct, string? playerId = null)
    {
        GameView view;
        lock (gate) {
            var questionVisible = role != "player" || phase == GamePhase.QuestionOpen && playerId is not null && players.TryGetValue(playerId, out var p) && !p.Player.Answered;
            view = new GameView(role, phase, players.Values.Select(x => new PlayerView(x.Player.Name, x.Player.Balance, x.Player.Answered, phase == GamePhase.QuestionClosed ? x.Player.Correct : null)).ToArray(),
                catalog.Subjects, selectedSubjects, phase == GamePhase.Lobby ? 0 : questionIndex + 1, ladder.TotalQuestions,
                questionIndex < 0 ? 0 : ladder.GetPrize(questionIndex), questionVisible ? current?.Text : null,
                questionVisible ? current?.Options : null, role == "host" ? deadline : null,
                role == "host" && phase == GamePhase.QuestionClosed ? current?.CorrectOption : null, notice);
        }
        await SendMessageAsync(socket, new { type = "state", state = view }, ct);
    }

    private async Task BroadcastAsync(CancellationToken ct = default)
    {
        WebSocket? hostSocket; KeyValuePair<string, (Player Player, WebSocket Socket)>[] audience;
        lock (gate) { hostSocket = host; audience = players.ToArray(); }
        if (hostSocket?.State == WebSocketState.Open) await SendViewAsync(hostSocket, "host", ct);
        foreach (var (id, entry) in audience) if (entry.Socket.State == WebSocketState.Open) await SendViewAsync(entry.Socket, "player", ct, id);
    }

    private async Task DisconnectAsync(WebSocket socket, string role, string? id)
    {
        if (role == "host") { lock (gate) if (ReferenceEquals(host, socket)) host = null; }
        if (role == "player" && id is not null) { lock (gate) if (phase == GamePhase.Lobby) players.Remove(id); }
        await BroadcastAsync();
        await CloseAsync(socket);
    }
    private string? FindPlayerId(WebSocket socket) { lock (gate) return players.FirstOrDefault(p => ReferenceEquals(p.Value.Socket, socket)).Key; }
    private async Task<JsonElement?> ReceiveAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096]; using var stream = new MemoryStream();
        while (true) {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > 4096) return null;
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
        }
    }
    private async Task SendMessageAsync(WebSocket socket, object message, CancellationToken ct = default)
    {
        if (socket.State != WebSocketState.Open) return;
        var gate = sendLocks.GetOrAdd(socket, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { var bytes = JsonSerializer.SerializeToUtf8Bytes(message, jsonOptions); await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { logger.LogDebug(ex, "Envio WebSocket interrompido"); }
        finally { gate.Release(); }
    }
    private static string ReadString(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static string[] ReadStringArray(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Take(20).ToArray() : [];
    private static int ReadInt(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : -1;
    private static async Task CloseAsync(WebSocket socket) { try { if (socket.State == WebSocketState.Open) await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Encerrado", CancellationToken.None); } catch (WebSocketException) { } }
}

public sealed class GameClock(GameSession game, ILogger<GameClock> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await game.TickAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Erro no relógio da partida"); }
    }
}