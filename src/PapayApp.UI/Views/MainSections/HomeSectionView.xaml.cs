using PapayApp.UI.Services;
namespace PapayApp.UI.Views.MainSections;
public partial class HomeSectionView : ContentView
{
    public HomeSectionView() { InitializeComponent(); }
    private async void OnOpenRecent(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string directory }) return;
        try { await new FolderLauncherService().OpenFolderAsync(directory); }
        catch (Exception ex) { if (Application.Current?.Windows.FirstOrDefault()?.Page is { } page) await page.DisplayAlertAsync("Carpeta no disponible", UserProblem.Describe(ex, "abrir el trabajo reciente"), "Entendido"); }
    }
}