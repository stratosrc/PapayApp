using System.Collections.ObjectModel;
using System.Windows.Input;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed partial class HomeViewModel
{
    public string SelectedEvaluationText => _selectedEvaluationType.HasValue ? "Seleccionada: " + _selectedEvaluationType.Value.ToString() : "Selecciona un tipo para comenzar.";
    public ResultActionsViewModel Results { get; private set; } = null!;
    public IReadOnlyList<RecentWork> RecentWorks => RecentWorkStore.Load();
    public bool HasRecentWorks => RecentWorks.Count > 0;
    public ICommand OpenBaseDirectoryCommand { get; private set; } = null!;
    public ICommand CancelGenerationCommand { get; private set; } = null!;
    public ObservableCollection<DirectoryCrumb> DirectoryCrumbs { get; } = [];
    private readonly List<DirectoryEntryViewModel> _directoryItems = [];
    private string _directorySearch = "";
    public string DirectorySearch
    {
        get => _directorySearch;
        set { if (SetProperty(ref _directorySearch, value)) FilterDirectories(); }
    }
    private void InitializeWorkspace()
    {
        Results = new(_folderLauncherService, () =>
        {
            foreach (var row in _allPreviewRecords) row.IsSelected = false;
            GenerationSummaryText = "";
            StatusMessage = "Nuevo lote: selecciona las personas o pulsa Cambiar archivo maestro para cargar otro Excel.";
        });
        OpenBaseDirectoryCommand = new AsyncRelayCommand(async () =>
        {
            try { await _folderLauncherService.OpenFolderAsync(_baseDirectoryService.BaseDirectoryPath ?? ""); }
            catch (Exception e) { await _appDialogService.ConfirmAsync("Ubicación de resultados", UserProblem.Describe(e, "abrir la carpeta base"), "Entendido", "Cerrar"); }
        });
        CancelGenerationCommand = new RelayCommand(() => _generationCancellationTokenSource?.Cancel());
        RecentWorkStore.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        { OnPropertyChanged(nameof(RecentWorks)); OnPropertyChanged(nameof(HasRecentWorks)); });
    }
    private void FilterDirectories()
    {
        DirectoryEntries.Clear();
        foreach (var item in _directoryItems.Where(i => i.Name.Contains(DirectorySearch, StringComparison.CurrentCultureIgnoreCase))) DirectoryEntries.Add(item);
        OnPropertyChanged(nameof(HasDirectoryEntries));
    }
    private void UpdateCrumbs(string root, string current)
    {
        DirectoryCrumbs.Clear();
        DirectoryCrumbs.Add(new("Base", new AsyncRelayCommand(() => LoadDirectoryEntriesAsync(root))));
        var path = root;
        var relative = Path.GetRelativePath(root, current);
        if (relative == ".") return;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar))
        {
            path = Path.Combine(path, part);
            var target = path;
            DirectoryCrumbs.Add(new(part, new AsyncRelayCommand(() => LoadDirectoryEntriesAsync(target))));
        }
    }
}
public sealed record DirectoryCrumb(string Name, ICommand OpenCommand);
