namespace PapayApp.UI.ViewModels;

public sealed partial class HomeViewModel
{
    private int GetReadyFolderCount()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            return 0;
        }

        return RequiredBaseFolders.Count(folderName => Directory.Exists(Path.Combine(baseDirectory, folderName)));
    }

    private void RefreshSettingsState()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        SettingsBaseDirectoryPathText = string.IsNullOrWhiteSpace(baseDirectory)
            ? "No hay una ruta base configurada."
            : baseDirectory;
        OnPropertyChanged(nameof(HomeBaseDirectoryText));
        OnPropertyChanged(nameof(HomePrimaryCounterText));
        OnPropertyChanged(nameof(HomeSecondaryCounterText));
    }

    private void UpdateClock()
    {
        DateText = _dateService.GetWelcomeDateText();
        TimeText = DateTime.Now.ToString("HH:mm");
    }

    private void SelectSection(MainSection section)
    {
        if (_selectedSection == section)
        {
            return;
        }

        _selectedSection = section;
        OnSectionChanged();
        SectionChanged?.Invoke(this, section);

        if (section == MainSection.Folders)
        {
            _ = RefreshDirectoriesAsync();
        }

        if (section == MainSection.Settings || section == MainSection.Home)
        {
            RefreshSettingsState();
        }
    }

    private void OnSectionChanged()
    {
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsDocumentsVisible));
        OnPropertyChanged(nameof(IsCertificatesVisible));
        OnPropertyChanged(nameof(IsFoldersVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(HomeButtonBackground));
        OnPropertyChanged(nameof(DocumentsButtonBackground));
        OnPropertyChanged(nameof(CertificatesButtonBackground));
        OnPropertyChanged(nameof(FoldersButtonBackground));
        OnPropertyChanged(nameof(SettingsButtonBackground));
        OnPropertyChanged(nameof(HomeTextColor));
        OnPropertyChanged(nameof(DocumentsTextColor));
        OnPropertyChanged(nameof(CertificatesTextColor));
        OnPropertyChanged(nameof(FoldersTextColor));
        OnPropertyChanged(nameof(SettingsTextColor));
        OnPropertyChanged(nameof(HomeIconOpacity));
        OnPropertyChanged(nameof(DocumentsIconOpacity));
        OnPropertyChanged(nameof(CertificatesIconOpacity));
        OnPropertyChanged(nameof(FoldersIconOpacity));
        OnPropertyChanged(nameof(SettingsIconOpacity));
    }

    private Color GetButtonBackground(MainSection section)
    {
        return _selectedSection == section ? SelectedBackground : IdleBackground;
    }

    private Color GetTextColor(MainSection section)
    {
        return _selectedSection == section ? SelectedText : IdleText;
    }

    private double GetIconOpacity(MainSection section)
    {
        return _selectedSection == section ? 1 : 0.72;
    }
}
