namespace RespondaRapido.Domain;
public sealed record QuizQuestion(int Id, int Number, string Text, string[] Options, int CorrectOption, char Difficulty, string SubjectCode);
public sealed record Subject(string Code, string Name);
public sealed record Player(string Id, string Name, int Balance, bool Answered, bool? Correct, int? SelectedOption);
public enum GamePhase { Lobby, QuestionOpen, QuestionClosed, Finished }
public sealed record PlayerView(string Name, int Balance, bool Answered, bool? Correct);
public sealed record GameView(string Role, GamePhase Phase, IReadOnlyList<PlayerView> Players, IReadOnlyList<Subject> Subjects,
 string[] SelectedSubjects, int QuestionNumber, int TotalQuestions, int Prize, string? Question, string[]? Options,
 DateTimeOffset? Deadline, int? CorrectOption, string? Notice);
public sealed class QuizSettings {
 public const string SectionName = "Quiz";
 public int AnswerSeconds { get; set; } = 20;
 public string QuestionsFile { get; set; } = "perguntas.csv";
 public int LobbyNameMinLength { get; set; } = 2;
 public int LobbyNameMaxLength { get; set; } = 24;
 public bool OpenBrowserOnStart { get; set; } = true;
 public string BrowserUrl { get; set; } = "http://localhost:5078/";
}