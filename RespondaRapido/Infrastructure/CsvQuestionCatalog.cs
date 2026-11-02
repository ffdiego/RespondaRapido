using System.Text;
using Microsoft.Extensions.Options;
using RespondaRapido.Domain;
namespace RespondaRapido.Infrastructure;
public interface IQuestionCatalog { IReadOnlyList<QuizQuestion> Questions { get; } IReadOnlyList<Subject> Subjects { get; } int InvalidRows { get; } }
public sealed class CsvQuestionCatalog : IQuestionCatalog {
 private static readonly Subject[] KnownSubjects = [new("C","Ciências"),new("G","Geografia"),new("H","História"),new("I","Inglês"),new("M","Matemática"),new("P","Português"),new("V","Variedades")];
 public IReadOnlyList<QuizQuestion> Questions { get; }
 public IReadOnlyList<Subject> Subjects => KnownSubjects;
 public int InvalidRows { get; }
 public CsvQuestionCatalog(IWebHostEnvironment env, IOptions<QuizSettings> settings, ILogger<CsvQuestionCatalog> logger) {
  var path = Path.IsPathRooted(settings.Value.QuestionsFile) ? settings.Value.QuestionsFile : Path.Combine(env.ContentRootPath,settings.Value.QuestionsFile);
  if (!File.Exists(path)) throw new FileNotFoundException("Arquivo de perguntas não encontrado.",path);
  var rows = new List<QuizQuestion>(); var invalid = 0;
  foreach (var line in File.ReadLines(path).Skip(1)) {
   if (!TryParseRow(line, out var c)) { invalid++; continue; }
   if(c.Length!=9 || !int.TryParse(c[0],out var n) || !int.TryParse(c[6],out var a) || a<1 || a>4 || c.Skip(1).Take(5).Any(string.IsNullOrWhiteSpace) || c[7].Length!=1 || !"ABCD".Contains(c[7][0]) || c[8].Length!=1 || !KnownSubjects.Any(s=>s.Code==c[8])) { invalid++; continue; }
   rows.Add(new QuizQuestion(rows.Count,n,c[1].Trim(),[c[2].Trim(),c[3].Trim(),c[4].Trim(),c[5].Trim()],a-1,c[7][0],c[8]));
  }
  Questions=rows; InvalidRows=invalid; logger.LogInformation("Banco carregado: {Valid} perguntas válidas; {Invalid} linhas ignoradas",rows.Count,invalid);
 }
 private static bool TryParseRow(string line, out string[] cells) {
  var fields = new List<string>(); var field = new StringBuilder(); var quoted = false;
  for (var i = 0; i < line.Length; i++) {
   var ch = line[i];
   if (ch == '"') {
    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
    else quoted = !quoted;
   } else if (ch == ';' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
   else field.Append(ch);
  }
  if (quoted) { cells = []; return false; }
  fields.Add(field.ToString()); cells = fields.ToArray(); return true;
 }
}