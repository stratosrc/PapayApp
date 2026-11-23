using PapayApp.Core.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.Views.MainSections;
public partial class SettingsSectionView : ContentView
{
    private bool _ready;
    public void ConfigureLaboratoryTemplates(ILaboratoryTemplateStore store, IFileSelectionService files) => LaboratoryTemplates.Configure(store, files);
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
    private void OnConfigurationClicked(object? sender, EventArgs e) => SelectHelp(false);
    private void OnHelpClicked(object? sender, EventArgs e) => SelectHelp(true);

    private void SelectHelp(bool help)
    {
        ConfigurationPanel.IsVisible = !help;
        HelpPanel.IsVisible = help;
        ConfigurationTab.SetDynamicResource(StyleProperty, help ? "SecondaryAction" : "PrimaryAction");
        HelpTab.SetDynamicResource(StyleProperty, help ? "PrimaryAction" : "SecondaryAction");
        SemanticProperties.SetDescription(ConfigurationTab, help ? "Abrir la pestaña Configuración" : "Configuración, pestaña seleccionada");
        SemanticProperties.SetDescription(HelpTab, help ? "Ayuda, pestaña seleccionada" : "Abrir la pestaña Ayuda");
    }
}
