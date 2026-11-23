namespace PapayApp.UI.Services;

public static class UiTypography
{
    public static IReadOnlyDictionary<string, double> Sizes(string size)
    {
        var scale = size switch { "Mediano" => 1.1, "Grande" => 1.2, _ => 1d };
        return new Dictionary<string, double>
        {
            ["PageTitleFontSize"] = Math.Round(28 * scale, 1),
            ["SectionTitleFontSize"] = Math.Round(18 * scale, 1),
            ["BodyFontSize"] = Math.Round(14 * scale, 1),
            ["SmallFontSize"] = Math.Round(13 * scale, 1)
        };
    }
}
