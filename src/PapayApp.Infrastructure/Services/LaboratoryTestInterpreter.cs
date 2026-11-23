using System.Text.RegularExpressions;

namespace PapayApp.Infrastructure.Services;

public static class LaboratoryTestInterpreter
{
    public static readonly string[] Analytes = ["anfetaminas", "barbituricos", "benzodiazepinas", "cocaina", "marihuana", "metadona", "morfina"];
    private static readonly IReadOnlyDictionary<string, string> Codes = new Dictionary<string, string>
    {
        ["AMP"] = "anfetaminas", ["BAR"] = "barbituricos", ["COC"] = "cocaina", ["THC"] = "marihuana"
    };
    public static IReadOnlyDictionary<string, string>? Interpret(string raw)
    {
        var text = LaboratoryImportService.Normalize(raw);
        if (text == "NO REACTIVO") return Analytes.ToDictionary(a => a, _ => "Negativo");
        if (!text.StartsWith("REACTIVO ", StringComparison.Ordinal)) return null;
        var words = Regex.Split(text[9..].Trim(), @"\s+").Where(s => s.Length > 0).ToList();
        if (words.Count == 0 || words.Any(w => !Codes.ContainsKey(w))) return null;
        var positives = words.Select(w => Codes[w]).ToHashSet(StringComparer.Ordinal);
        return Analytes.ToDictionary(a => a, a => positives.Contains(a) ? "Positivo" : "Negativo");
    }
}
