namespace PapayApp.UI.Views.MainSections;
public partial class SettingsSectionView : ContentView
{
    private bool _ready;
    public SettingsSectionView()
    {
        InitializeComponent();
        ThemePicker.ItemsSource = new[] { "Oscuro", "Claro" };
        SizePicker.ItemsSource = new[] { "Normal", "Mediano", "Grande" };
        ThemePicker.SelectedItem = Preferences.Default.Get("UiTheme", "Oscuro");
        SizePicker.SelectedItem = Preferences.Default.Get("UiSize", "Normal");
        _ready = true;
    }
    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        if (_ready) Services.AppearanceService.Apply((string)ThemePicker.SelectedItem, (string)SizePicker.SelectedItem);
    }
}
