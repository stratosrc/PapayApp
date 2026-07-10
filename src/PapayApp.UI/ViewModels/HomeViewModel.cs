using System.Windows.Input;
using PapayApp.Core.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed class HomeViewModel : BaseViewModel
{
    private static readonly Color SelectedBackground = Color.FromArgb("#2892D7");
    private static readonly Color IdleBackground = Color.FromArgb("#00173753");
    private static readonly Color SelectedText = Color.FromArgb("#F4FAFD");
    private static readonly Color IdleText = Color.FromArgb("#DCECF4");
    private static readonly Color SelectedEvaluationBackground = Color.FromArgb("#2F9E44");
    private static readonly Color IdleEvaluationBackground = Color.FromArgb("#F4FAFD");
    private static readonly Color IdleEvaluationText = Color.FromArgb("#000000");

    private MainSection _selectedSection = MainSection.Home;
    private MedicalEvaluationType? _selectedEvaluationType;
    private string? _selectedUploadFileName;

    public HomeViewModel(IDateService dateService, IFileSelectionService fileSelectionService)
    {
        DateText = dateService.GetWelcomeDateText();
        ShowHomeCommand = new RelayCommand(() => SelectSection(MainSection.Home));
        ShowDocumentsCommand = new RelayCommand(() => SelectSection(MainSection.Documents));
        ShowFoldersCommand = new RelayCommand(() => SelectSection(MainSection.Folders));
        ShowSettingsCommand = new RelayCommand(() => SelectSection(MainSection.Settings));
        SelectPeriodicaCommand = new RelayCommand(() => SelectEvaluationType(MedicalEvaluationType.Periodica));
        SelectPreocupacionalCommand = new RelayCommand(() => SelectEvaluationType(MedicalEvaluationType.Preocupacional));
        SelectReintegroCommand = new RelayCommand(() => SelectEvaluationType(MedicalEvaluationType.Reintegro));
        SelectRetiroCommand = new RelayCommand(() => SelectEvaluationType(MedicalEvaluationType.Retiro));
        PickUploadFileCommand = new AsyncRelayCommand(async () =>
        {
            var file = await fileSelectionService.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            SetUploadFileName(file.FileName);
        });
    }

    public string WelcomeTitle => "Bienvenida";

    public string DateText { get; }

    public string FoldersPlaceholder => "Aqui ira la vista de los directorios";

    public string SettingsPlaceholder => "Aqui iran ajustes";

    public ICommand ShowHomeCommand { get; }

    public ICommand ShowDocumentsCommand { get; }

    public ICommand ShowFoldersCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand SelectPeriodicaCommand { get; }

    public ICommand SelectPreocupacionalCommand { get; }

    public ICommand SelectReintegroCommand { get; }

    public ICommand SelectRetiroCommand { get; }

    public ICommand PickUploadFileCommand { get; }

    public bool IsHomeVisible => _selectedSection == MainSection.Home;

    public bool IsDocumentsVisible => _selectedSection == MainSection.Documents;

    public bool IsFoldersVisible => _selectedSection == MainSection.Folders;

    public bool IsSettingsVisible => _selectedSection == MainSection.Settings;

    public Color HomeButtonBackground => GetButtonBackground(MainSection.Home);

    public Color DocumentsButtonBackground => GetButtonBackground(MainSection.Documents);

    public Color FoldersButtonBackground => GetButtonBackground(MainSection.Folders);

    public Color SettingsButtonBackground => GetButtonBackground(MainSection.Settings);

    public Color HomeTextColor => GetTextColor(MainSection.Home);

    public Color DocumentsTextColor => GetTextColor(MainSection.Documents);

    public Color FoldersTextColor => GetTextColor(MainSection.Folders);

    public Color SettingsTextColor => GetTextColor(MainSection.Settings);

    public double HomeIconOpacity => GetIconOpacity(MainSection.Home);

    public double DocumentsIconOpacity => GetIconOpacity(MainSection.Documents);

    public double FoldersIconOpacity => GetIconOpacity(MainSection.Folders);

    public double SettingsIconOpacity => GetIconOpacity(MainSection.Settings);

    public string UploadAreaTitle => HasSelectedUploadFile ? SelectedUploadFileName! : "Subir archivo";

    public string UploadAreaSubtitle => HasSelectedUploadFile
        ? "1 archivo seleccionado"
        : "Haz clic o arrastra aqui un archivo. Limite: 1";

    public string? SelectedUploadFileName
    {
        get => _selectedUploadFileName;
        private set
        {
            if (_selectedUploadFileName == value)
            {
                return;
            }

            _selectedUploadFileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UploadAreaTitle));
            OnPropertyChanged(nameof(UploadAreaSubtitle));
            OnPropertyChanged(nameof(HasSelectedUploadFile));
        }
    }

    public bool HasSelectedUploadFile => !string.IsNullOrWhiteSpace(SelectedUploadFileName);

    public void SetUploadFileName(string fileName)
    {
        SelectedUploadFileName = fileName;
    }

    public Color PeriodicaButtonBackground => GetEvaluationBackground(MedicalEvaluationType.Periodica);

    public Color PreocupacionalButtonBackground => GetEvaluationBackground(MedicalEvaluationType.Preocupacional);

    public Color ReintegroButtonBackground => GetEvaluationBackground(MedicalEvaluationType.Reintegro);

    public Color RetiroButtonBackground => GetEvaluationBackground(MedicalEvaluationType.Retiro);

    public Color PeriodicaTextColor => GetEvaluationTextColor(MedicalEvaluationType.Periodica);

    public Color PreocupacionalTextColor => GetEvaluationTextColor(MedicalEvaluationType.Preocupacional);

    public Color ReintegroTextColor => GetEvaluationTextColor(MedicalEvaluationType.Reintegro);

    public Color RetiroTextColor => GetEvaluationTextColor(MedicalEvaluationType.Retiro);

    private void SelectSection(MainSection section)
    {
        if (_selectedSection == section)
        {
            return;
        }

        _selectedSection = section;
        OnSectionChanged();
    }

    private void OnSectionChanged()
    {
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsDocumentsVisible));
        OnPropertyChanged(nameof(IsFoldersVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(HomeButtonBackground));
        OnPropertyChanged(nameof(DocumentsButtonBackground));
        OnPropertyChanged(nameof(FoldersButtonBackground));
        OnPropertyChanged(nameof(SettingsButtonBackground));
        OnPropertyChanged(nameof(HomeTextColor));
        OnPropertyChanged(nameof(DocumentsTextColor));
        OnPropertyChanged(nameof(FoldersTextColor));
        OnPropertyChanged(nameof(SettingsTextColor));
        OnPropertyChanged(nameof(HomeIconOpacity));
        OnPropertyChanged(nameof(DocumentsIconOpacity));
        OnPropertyChanged(nameof(FoldersIconOpacity));
        OnPropertyChanged(nameof(SettingsIconOpacity));
    }

    private void SelectEvaluationType(MedicalEvaluationType evaluationType)
    {
        if (_selectedEvaluationType == evaluationType)
        {
            return;
        }

        _selectedEvaluationType = evaluationType;
        OnPropertyChanged(nameof(PeriodicaButtonBackground));
        OnPropertyChanged(nameof(PreocupacionalButtonBackground));
        OnPropertyChanged(nameof(ReintegroButtonBackground));
        OnPropertyChanged(nameof(RetiroButtonBackground));
        OnPropertyChanged(nameof(PeriodicaTextColor));
        OnPropertyChanged(nameof(PreocupacionalTextColor));
        OnPropertyChanged(nameof(ReintegroTextColor));
        OnPropertyChanged(nameof(RetiroTextColor));
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

    private Color GetEvaluationBackground(MedicalEvaluationType evaluationType)
    {
        return _selectedEvaluationType == evaluationType ? SelectedEvaluationBackground : IdleEvaluationBackground;
    }

    private Color GetEvaluationTextColor(MedicalEvaluationType evaluationType)
    {
        return _selectedEvaluationType == evaluationType ? SelectedText : IdleEvaluationText;
    }
}
