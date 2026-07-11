using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed partial class HomeViewModel : BaseViewModel
{
    private static readonly string[] IngresoWorksheetKeywords = { "INGRESO", "INGRESOS", "ALTA", "ALTAS" };
    private static readonly string[] RetiroWorksheetKeywords = { "RETIRO", "RETIROS", "SALIDA", "SALIDAS", "BAJA", "BAJAS", "LIQUIDACION", "LIQUIDACIONES" };
    private static readonly string[] RequiredBaseFolders =
    {
        "EVALUACION MEDICA - PERIODICA",
        "EVALUACION MEDICA - PREOCUPACIONAL",
        "EVALUACION MEDICA - REINTEGRO",
        "EVALUACION MEDICA - RETIRO"
    };

    private static readonly Color SelectedBackground = Color.FromArgb("#2892D7");
    private static readonly Color IdleBackground = Color.FromArgb("#00173753");
    private static readonly Color SelectedText = Color.FromArgb("#F4FAFD");
    private static readonly Color IdleText = Color.FromArgb("#DCECF4");
    private static readonly Color SelectedEvaluationBackground = Color.FromArgb("#2F9E44");
    private static readonly Color IdleEvaluationBackground = Color.FromArgb("#F4FAFD");
    private static readonly Color IdleEvaluationText = Color.FromArgb("#000000");

    private readonly IDateService _dateService;
    private readonly IFileSelectionService _fileSelectionService;
    private readonly IAppDialogService _appDialogService;
    private readonly IBaseDirectoryService _baseDirectoryService;
    private readonly IFolderLauncherService _folderLauncherService;
    private readonly IExcelImportService _excelImportService;
    private readonly IMedicalEvaluationGenerationService _generationService;
    private readonly IOutputOrganizationService _outputOrganizationService;
    private readonly List<PreviewRecordRowViewModel> _allPreviewRecords = [];
    private MainSection _selectedSection = MainSection.Home;
    private OccupationalEvaluationType? _selectedEvaluationType;
    private string? _selectedUploadFileName;
    private string? _selectedTemplatePath;
    private string? _masterFilePath;
    private string? _selectedWorksheet;
    private bool _isBusy;
    private bool _isGenerating;
    private string _statusMessage = string.Empty;
    private string _dateText = string.Empty;
    private string _timeText = string.Empty;
    private string _previewSearchText = string.Empty;
    private string _importSummaryText = "Selecciona un archivo maestro para comenzar.";
    private string _generationSummaryText = string.Empty;
    private string _preGenerationSummaryText = string.Empty;
    private string _foldersStatusMessage = "Selecciona la carpeta Directorios para explorar la ruta base.";
    private string _foldersCurrentPathText = "Ruta actual: -";
    private string? _foldersCurrentDirectoryPath;
    private string _settingsBaseDirectoryPathText = "No hay una ruta base configurada.";
    private string _settingsStatusMessage = "Selecciona una carpeta para guardar y explorar los archivos generados.";
    private readonly System.Timers.Timer _clockTimer;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _generationCancellationTokenSource;

    public HomeViewModel(
        IDateService dateService,
        IFileSelectionService fileSelectionService,
        IAppDialogService appDialogService,
        IBaseDirectoryService baseDirectoryService,
        IFolderLauncherService folderLauncherService,
        IExcelImportService excelImportService,
        IMedicalEvaluationGenerationService generationService,
        IOutputOrganizationService outputOrganizationService)
    {
        _dateService = dateService;
        _fileSelectionService = fileSelectionService;
        _appDialogService = appDialogService;
        _baseDirectoryService = baseDirectoryService;
        _folderLauncherService = folderLauncherService;
        _excelImportService = excelImportService;
        _generationService = generationService;
        _outputOrganizationService = outputOrganizationService;

        _dateText = dateService.GetWelcomeDateText();
        _timeText = DateTime.Now.ToString("HH:mm");
        AvailableWorksheets = new ObservableCollection<string>();
        PreviewRecords = new ObservableCollection<PreviewRecordRowViewModel>();
        DirectoryEntries = new ObservableCollection<DirectoryEntryViewModel>();

        ShowHomeCommand = new RelayCommand(() => SelectSection(MainSection.Home));
        ShowDocumentsCommand = new RelayCommand(() => SelectSection(MainSection.Documents));
        ShowFoldersCommand = new RelayCommand(() => SelectSection(MainSection.Folders));
        ShowSettingsCommand = new RelayCommand(() => SelectSection(MainSection.Settings));
        SelectPeriodicaCommand = new RelayCommand(() => SelectEvaluationType(OccupationalEvaluationType.Periodica));
        SelectPreocupacionalCommand = new RelayCommand(() => SelectEvaluationType(OccupationalEvaluationType.Preocupacional));
        SelectReintegroCommand = new RelayCommand(() => SelectEvaluationType(OccupationalEvaluationType.Reintegro));
        SelectRetiroCommand = new RelayCommand(() => SelectEvaluationType(OccupationalEvaluationType.Retiro));
        PickUploadFileCommand = new AsyncRelayCommand(PickMasterFileAsync);
        GenerateFilesCommand = new AsyncRelayCommand(GenerateFilesAsync);
        ChangeMasterFileCommand = new AsyncRelayCommand(PickMasterFileAsync);
        ChangeTemplateCommand = new AsyncRelayCommand(PickTemplateFileAsync);
        RefreshDirectoriesCommand = new AsyncRelayCommand(RefreshDirectoriesAsync);
        NavigateUpDirectoryCommand = new AsyncRelayCommand(NavigateUpDirectoryAsync);
        OpenCurrentDirectoryCommand = new AsyncRelayCommand(OpenCurrentDirectoryAsync);
        ChangeBaseDirectoryCommand = new AsyncRelayCommand(ChangeBaseDirectoryAsync);
        RefreshSettingsState();

        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateClock);
        _clockTimer.AutoReset = true;
        _clockTimer.Start();
    }

    public event EventHandler<string>? GenerationCompleted;

    public event EventHandler<MainSection>? SectionChanged;

    public string WelcomeTitle => "Bienvenida";

    public string WelcomeSubtitle => "Generacion de evaluaciones medicas";

    public string DateText
    {
        get => _dateText;
        private set => SetProperty(ref _dateText, value);
    }

    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public bool IsWorkOverlayVisible => IsBusy || IsGenerating;

    public string WorkOverlayTitle => IsGenerating
        ? "Generando archivos"
        : "Cargando archivo maestro";

    public string WorkOverlayMessage => IsGenerating
        ? (string.IsNullOrWhiteSpace(StatusMessage) ? "Estamos preparando los archivos individuales." : StatusMessage)
        : (string.IsNullOrWhiteSpace(StatusMessage) ? "Estamos leyendo y validando la informacion del archivo." : StatusMessage);

    public string HomeBaseDirectoryText
    {
        get
        {
            var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
            return string.IsNullOrWhiteSpace(baseDirectory)
                ? "Ruta base: no configurada"
                : $"Ruta base: {baseDirectory}";
        }
    }

    public string HomePrimaryCounterText => string.IsNullOrWhiteSpace(_baseDirectoryService.BaseDirectoryPath)
        ? "Ruta base pendiente"
        : "Ruta base configurada";

    public string HomeSecondaryCounterText => $"{GetReadyFolderCount()} carpetas listas";

    public string FoldersTitle => "Directorios";

    public string SettingsTitle => "Ajustes";

    public ICommand ShowHomeCommand { get; }

    public ICommand ShowDocumentsCommand { get; }

    public ICommand ShowFoldersCommand { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand SelectPeriodicaCommand { get; }

    public ICommand SelectPreocupacionalCommand { get; }

    public ICommand SelectReintegroCommand { get; }

    public ICommand SelectRetiroCommand { get; }

    public ICommand PickUploadFileCommand { get; }

    public ICommand GenerateFilesCommand { get; }

    public ICommand ChangeMasterFileCommand { get; }

    public ICommand ChangeTemplateCommand { get; }

    public ICommand RefreshDirectoriesCommand { get; }

    public ICommand NavigateUpDirectoryCommand { get; }

    public ICommand OpenCurrentDirectoryCommand { get; }

    public ICommand ChangeBaseDirectoryCommand { get; }

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

    public string UploadAreaTitle => HasSelectedUploadFile ? SelectedUploadFileName! : "Subir archivo maestro";

    public string UploadAreaSubtitle => HasSelectedUploadFile
        ? "Archivo maestro cargado."
        : "Haz clic para elegir un archivo Excel maestro (.xlsx)";

    public string TemplateDisplayName => Path.GetFileName(CurrentTemplatePath);

    public string CurrentTemplatePath =>
        string.IsNullOrWhiteSpace(_selectedTemplatePath)
            ? Path.Combine(AppContext.BaseDirectory, "Templates", "FICHA MEDICA.xlsx")
            : _selectedTemplatePath;

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
            OnPropertyChanged(nameof(ShowUploadArea));
        }
    }

    public bool HasSelectedUploadFile => !string.IsNullOrWhiteSpace(SelectedUploadFileName);

    public bool ShowUploadArea => !HasSelectedUploadFile;

    public ObservableCollection<string> AvailableWorksheets { get; }

    public bool HasMultipleWorksheets => AvailableWorksheets.Count > 1;

    public string? SelectedWorksheet
    {
        get => _selectedWorksheet;
        set
        {
            if (!SetProperty(ref _selectedWorksheet, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanGenerate));

            if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(_masterFilePath))
            {
                _ = LoadSelectedWorksheetAsync();
            }
        }
    }

    public ObservableCollection<PreviewRecordRowViewModel> PreviewRecords { get; }

    public string PreviewSearchText
    {
        get => _previewSearchText;
        set
        {
            if (!SetProperty(ref _previewSearchText, value))
            {
                return;
            }

            ApplyPreviewFilter();
        }
    }

    public ObservableCollection<DirectoryEntryViewModel> DirectoryEntries { get; }

    public string ImportSummaryText
    {
        get => _importSummaryText;
        private set => SetProperty(ref _importSummaryText, value);
    }

    public string GenerationSummaryText
    {
        get => _generationSummaryText;
        private set
        {
            if (SetProperty(ref _generationSummaryText, value))
            {
                OnPropertyChanged(nameof(HasGenerationSummary));
            }
        }
    }

    public string PreGenerationSummaryText
    {
        get => _preGenerationSummaryText;
        private set
        {
            if (SetProperty(ref _preGenerationSummaryText, value))
            {
                OnPropertyChanged(nameof(HasPreGenerationSummary));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string FoldersStatusMessage
    {
        get => _foldersStatusMessage;
        private set => SetProperty(ref _foldersStatusMessage, value);
    }

    public string FoldersCurrentPathText
    {
        get => _foldersCurrentPathText;
        private set => SetProperty(ref _foldersCurrentPathText, value);
    }

    public bool HasDirectoryEntries => DirectoryEntries.Count > 0;

    public bool HasGenerationSummary => !string.IsNullOrWhiteSpace(GenerationSummaryText);

    public bool HasPreGenerationSummary => !string.IsNullOrWhiteSpace(PreGenerationSummaryText);

    public string PreviewResultsCountText => $"Mostrando {PreviewRecords.Count} personas";

    public string SettingsBaseDirectoryPathText
    {
        get => _settingsBaseDirectoryPathText;
        private set => SetProperty(ref _settingsBaseDirectoryPathText, value);
    }

    public string SettingsStatusMessage
    {
        get => _settingsStatusMessage;
        private set => SetProperty(ref _settingsStatusMessage, value);
    }

    public bool CanNavigateUpDirectory =>
        !string.IsNullOrWhiteSpace(_foldersCurrentDirectoryPath) &&
        !string.Equals(
            NormalizeDirectoryPath(_foldersCurrentDirectoryPath),
            NormalizeDirectoryPath(_baseDirectoryService.BaseDirectoryPath),
            StringComparison.OrdinalIgnoreCase);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanGenerate));
                OnPropertyChanged(nameof(IsWorkOverlayVisible));
                OnPropertyChanged(nameof(WorkOverlayTitle));
                OnPropertyChanged(nameof(WorkOverlayMessage));
            }
        }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                OnPropertyChanged(nameof(CanGenerate));
                OnPropertyChanged(nameof(IsWorkOverlayVisible));
                OnPropertyChanged(nameof(WorkOverlayTitle));
                OnPropertyChanged(nameof(WorkOverlayMessage));
            }
        }
    }

    public bool CanGenerate =>
        !IsBusy &&
        !IsGenerating &&
        _selectedEvaluationType.HasValue &&
        !string.IsNullOrWhiteSpace(_masterFilePath) &&
        !string.IsNullOrWhiteSpace(SelectedWorksheet) &&
        _allPreviewRecords.Any(static row => row.CanGenerate && row.IsSelected);

    public Color PeriodicaButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Periodica);

    public Color PreocupacionalButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Preocupacional);

    public Color ReintegroButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Reintegro);

    public Color RetiroButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Retiro);

    public Color PeriodicaTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Periodica);

    public Color PreocupacionalTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Preocupacional);

    public Color ReintegroTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Reintegro);

    public Color RetiroTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Retiro);

}
