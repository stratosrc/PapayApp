using System.Windows.Input;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed class ResultActionsViewModel : BaseViewModel
{
    private readonly IFolderLauncherService _launcher;
    private string? _file, _folder, _log;
    private string _message = "";
    public ResultActionsViewModel(IFolderLauncherService launcher, Action reset)
    {
        _launcher = launcher;
        OpenFileCommand = new AsyncRelayCommand(() => OpenAsync(_file));
        OpenFolderCommand = new AsyncRelayCommand(() => OpenAsync(_folder));
        OpenErrorsCommand = new AsyncRelayCommand(() => OpenAsync(_log));
        NewBatchCommand = new RelayCommand(() => { Clear(); reset(); });
    }
    public bool HasFile => !string.IsNullOrEmpty(_file);
    public bool HasErrors => !string.IsNullOrEmpty(_log);
    public bool HasResult => !string.IsNullOrEmpty(_folder);
    public string FileActionText => string.Equals(Path.GetExtension(_file), ".pdf", StringComparison.OrdinalIgnoreCase) ? "Abrir PDF" : "Abrir archivo";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenErrorsCommand { get; }
    public ICommand NewBatchCommand { get; }
    public void Set(string? file, string folder, string? log)
    {
        _file = file; _folder = folder; _log = log; Message = "";
        foreach (var p in new[] { nameof(HasFile), nameof(HasErrors), nameof(HasResult), nameof(FileActionText) }) OnPropertyChanged(p);
    }
    public void Clear() { _file = _folder = _log = null; Message = ""; OnPropertyChanged(nameof(HasResult)); }
    private async Task OpenAsync(string? path)
    {
        if (path is null) return;
        try { await _launcher.OpenPathAsync(path); }
        catch (Exception e) { Message = UserProblem.Describe(e, "abrir el resultado"); }
    }
}
