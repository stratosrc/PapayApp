namespace PapayApp.UI.Services;

public static class AppearanceService
{
    public static void Apply(string theme, string size)
    {
        Preferences.Default.Set("UiTheme", theme);
        Preferences.Default.Set("UiSize", size);
        if (Application.Current is not { } app) return;
        app.UserAppTheme = theme == "Claro" ? AppTheme.Light : AppTheme.Dark;
        var light = theme == "Claro";
        var colors = new Dictionary<string, string>
        {
            ["PageBackground"] = light ? "#EEF3F7" : "#101820",
            ["SurfaceBackground"] = light ? "#FFFFFF" : "#192630",
            ["InputBackground"] = light ? "#E0EAF1" : "#223440",
            ["NavigationBackground"] = light ? "#DFE9F0" : "#111F2A",
            ["OverlayBackground"] = light ? "#EEF3F7" : "#EE101820",
            ["PrimaryText"] = light ? "#152C3C" : "#F0F5F9",
            ["SecondaryText"] = light ? "#40596B" : "#B5C6D3",
            ["PlaceholderText"] = light ? "#526B7D" : "#94A9BA",
            ["OutlineColor"] = light ? "#7890A2" : "#405868",
            ["DangerText"] = light ? "#A51D32" : "#FFB4B4",
            ["WarningText"] = light ? "#775000" : "#EAC48A"
        };
        foreach (var pair in colors) app.Resources[pair.Key] = Color.FromArgb(pair.Value);
        foreach (var pair in UiTypography.Sizes(size)) app.Resources[pair.Key] = pair.Value;
    }
}
