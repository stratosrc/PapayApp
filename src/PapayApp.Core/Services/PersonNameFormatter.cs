using System.Globalization;

namespace PapayApp.Core.Services;

public static class PersonNameFormatter
{
    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    public static string ToTitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedWhitespace = string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var lowercase = normalizedWhitespace.ToLower(SpanishCulture);
        return SpanishCulture.TextInfo.ToTitleCase(lowercase);
    }
}
