namespace RespondaRapido.Domain;
public interface IPrizeLadder { int TotalQuestions { get; } int GetPrize(int questionIndex); char GetDifficulty(int questionIndex); }
public sealed class PrizeLadder : IPrizeLadder {
 private static readonly int[] Prizes = [1000,2000,3000,4000,5000,10000,20000,30000,40000,50000,100000,200000,300000,400000,500000,1000000];
 public int TotalQuestions => Prizes.Length;
 public int GetPrize(int index) => Prizes[index];
 public char GetDifficulty(int index) => index < 5 ? 'A' : index < 10 ? 'B' : index < 15 ? 'C' : 'D';
}
public interface IScoringPolicy { int Apply(int currentBalance, bool correct, int prize); }
public sealed class CorrectAnswerScoringPolicy : IScoringPolicy { public int Apply(int currentBalance, bool correct, int prize) => correct ? currentBalance + prize : currentBalance; }
public interface IAnswerWindow { DateTimeOffset Open(DateTimeOffset now); bool IsExpired(DateTimeOffset deadline, DateTimeOffset now); }
public sealed class TimedAnswerWindow(Microsoft.Extensions.Options.IOptions<QuizSettings> settings) : IAnswerWindow {
 private readonly int seconds = Math.Clamp(settings.Value.AnswerSeconds, 5, 180);
 public DateTimeOffset Open(DateTimeOffset now) => now.AddSeconds(seconds);
 public bool IsExpired(DateTimeOffset deadline, DateTimeOffset now) => now >= deadline;
}
public interface IQuestionPicker { QuizQuestion Pick(IReadOnlyList<QuizQuestion> questions, char difficulty, IReadOnlySet<string> subjects, IReadOnlySet<int> used); }
public sealed class RandomQuestionPicker : IQuestionPicker {
 private readonly Random random = new();
 public QuizQuestion Pick(IReadOnlyList<QuizQuestion> questions, char difficulty, IReadOnlySet<string> subjects, IReadOnlySet<int> used) {
  var choices = questions.Where(q => q.Difficulty == difficulty && subjects.Contains(q.SubjectCode) && !used.Contains(q.Id)).ToArray();
  if (choices.Length == 0) throw new InvalidOperationException($"Não há perguntas disponíveis para dificuldade {difficulty} nas matérias escolhidas.");
  return choices[random.Next(choices.Length)];
 }
}