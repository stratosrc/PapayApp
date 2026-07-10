using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PapayApp.Core.Configuration;
using PapayApp.Core.Models;

namespace PapayApp.Infrastructure.Services;

public sealed class HeaderNormalizer
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex PunctuationRegex = new(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, EmployeeField> _aliasMap;

    public HeaderNormalizer()
    {
        _aliasMap = BuildAliasMap();
    }

    public string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = MultiWhitespaceRegex.Replace(value.Trim(), " ");
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var withoutDiacritics = builder.ToString().Normalize(NormalizationForm.FormC);
        var withoutPunctuation = PunctuationRegex.Replace(withoutDiacritics, " ");
        return MultiWhitespaceRegex.Replace(withoutPunctuation, " ").Trim().ToUpperInvariant();
    }

    public EmployeeField? ResolveField(string? header)
    {
        var normalized = Normalize(header);
        return _aliasMap.TryGetValue(normalized, out var field) ? field : null;
    }

    private IReadOnlyDictionary<string, EmployeeField> BuildAliasMap()
    {
        var map = new Dictionary<string, EmployeeField>();

        foreach (var pair in HeaderAliasConfiguration.Aliases)
        {
            var canonicalNormalized = Normalize(pair.Key.ToString());
            map[canonicalNormalized] = pair.Key;

            foreach (var alias in pair.Value)
            {
                map[Normalize(alias)] = pair.Key;
            }
        }

        return map;
    }
}
