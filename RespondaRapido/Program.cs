using RespondaRapido.Domain;
using RespondaRapido.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<QuizSettings>(builder.Configuration.GetSection(QuizSettings.SectionName));
builder.Services.AddSingleton<IQuestionCatalog, CsvQuestionCatalog>();
builder.Services.AddSingleton<IPrizeLadder, PrizeLadder>();
builder.Services.AddSingleton<IScoringPolicy, CorrectAnswerScoringPolicy>();
builder.Services.AddSingleton<IAnswerWindow, TimedAnswerWindow>();
builder.Services.AddSingleton<IQuestionPicker, RandomQuestionPicker>();
builder.Services.AddSingleton<GameSession>();
builder.Services.AddHostedService<GameClock>();
var app = builder.Build();
app.UseWebSockets(); app.UseDefaultFiles(); app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.MapGet("/api/materias", (IQuestionCatalog catalog) => catalog.Subjects);
app.Map("/ws", async (HttpContext context, GameSession game) => {
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await game.HandleConnectionAsync(socket, context.RequestAborted);
});
app.Run();