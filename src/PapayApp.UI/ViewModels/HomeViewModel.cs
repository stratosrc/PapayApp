using System.Collections.ObjectModel;
using System.Windows.Input;
using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
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

    private readonly IDateService _dateService;
    private readonly IFileSelectionService _fileSelectionService;
    private readonly IBaseDirectoryService _baseDirectoryService;
    private readonly IExcelImportService _excelImportService;
    private readonly IMedicalEvaluationGenerationService _generationService;
    private readonly IFolderLauncherService _folderLauncherService;

    private MainSection _selectedSection = MainSection.Home;
    private OccupationalEvaluationType? _selectedEvaluationType;
    private string? _selectedUploadFileName;
    private string? _masterFilePath;
    private string? _selectedWorksheet;
    private bool _isBusy;
    private bool _isGenerating;
    private string _statusMessage = string.Empty;
    private string _importSummaryText = "Selecciona un archivo maestro para comenzar.";
    private string _generationSummaryText = string.Empty;
    private string? _lastGeneratedOutputPath;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _generationCancellationTokenSource;

    public HomeViewModel(
        IDateService dateService,
        IFileSelectionService fileSelectionService,
        IBaseDirectoryService baseDirectoryService,
        IExcelImportService excelImportService,
        IMedicalEvaluationGenerationService generationService,
        IFolderLauncherService folderLauncherService)
    {
        _dateService = dateService;
        _fileSelectionService = fileSelectionService;
        _baseDirectoryService = baseDirectoryService;
        _excelImportService = excelImportService;
        _generationService = generationService;
        _folderLauncherService = folderLauncherService;

        DateText = dateService.GetWelcomeDateText();
        AvailableWorksheets = new ObservableCollection<string>();
        PreviewRecords = new ObservableCollection<PreviewRecordRowViewModel>();

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
        CancelGenerationCommand = new RelayCommand(CancelGeneration);
        OpenOutputFolderCommand = new AsyncRelayCommand(OpenOutputFolderAsync);
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

    public ICommand GenerateFilesCommand { get; }

    public ICommand CancelGenerationCommand { get; }

    public ICommand OpenOutputFolderCommand { get; }

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

    public string ImportSummaryText
    {
        get => _importSummaryText;
        private set => SetProperty(ref _importSummaryText, value);
    }

    public string GenerationSummaryText
    {
        get => _generationSummaryText;
        private set => SetProperty(ref _generationSummaryText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanGenerate));
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
            }
        }
    }

    public bool CanGenerate =>
        !IsBusy &&
        !IsGenerating &&
        _selectedEvaluationType.HasValue &&
        !string.IsNullOrWhiteSpace(_masterFilePath) &&
        !string.IsNullOrWhiteSpace(SelectedWorksheet) &&
        PreviewRecords.Any(static row => row.CanGenerate && row.IsSelected);

    public bool CanOpenOutputFolder => !string.IsNullOrWhiteSpace(_lastGeneratedOutputPath) && Directory.Exists(_lastGeneratedOutputPath);

    public Color PeriodicaButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Periodica);

    public Color PreocupacionalButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Preocupacional);

    public Color ReintegroButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Reintegro);

    public Color RetiroButtonBackground => GetEvaluationBackground(OccupationalEvaluationType.Retiro);

    public Color PeriodicaTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Periodica);

    public Color PreocupacionalTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Preocupacional);

    public Color ReintegroTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Reintegro);

    public Color RetiroTextColor => GetEvaluationTextColor(OccupationalEvaluationType.Retiro);

    public async Task LoadMasterFileAsync(string filePath, string? displayName = null)
    {
        _masterFilePath = filePath;
        SelectedUploadFileName = displayName ?? Path.GetFileName(filePath);
        AvailableWorksheets.Clear();
        OnPropertyChanged(nameof(HasMultipleWorksheets));
        PreviewRecords.Clear();
        ImportSummaryText = "Leyendo archivo maestro...";
        GenerationSummaryText = string.Empty;

        try
        {
            IsBusy = true;
            var worksheets = await _excelImportService.GetWorksheetNamesAsync(filePath, CancellationToken.None);

            foreach (var worksheet in worksheets)
            {
                AvailableWorksheets.Add(worksheet);
            }

            OnPropertyChanged(nameof(HasMultipleWorksheets));
            SelectedWorksheet = AvailableWorksheets.FirstOrDefault();
            StatusMessage = worksheets.Count == 0
                ? "El archivo maestro no contiene hojas utilizables."
                : "Archivo maestro cargado correctamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PickMasterFileAsync()
    {
        try
        {
            StatusMessage = string.Empty;
            var file = await _fileSelectionService.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            await LoadMasterFileAsync(file.FullPath, file.FileName);
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible abrir el archivo maestro: {exception.Message}";
        }
    }

    private async Task LoadSelectedWorksheetAsync()
    {
        if (string.IsNullOrWhiteSpace(_masterFilePath) || string.IsNullOrWhiteSpace(SelectedWorksheet))
        {
            return;
        }

        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            StatusMessage = "Leyendo la hoja seleccionada...";

            var preview = await _excelImportService.ImportWorksheetAsync(
                _masterFilePath,
                SelectedWorksheet,
                _previewCancellationTokenSource.Token);

            PreviewRecords.Clear();
            foreach (var record in preview.Records)
            {
                var rowViewModel = new PreviewRecordRowViewModel(record)
                {
                    IsSelected = record.Status != ImportRecordStatus.Omitted
                };
                rowViewModel.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanGenerate));
                PreviewRecords.Add(rowViewModel);
            }

            ImportSummaryText =
                $"Hoja: {preview.WorksheetName} · Válidos: {preview.ValidCount} · Incompletos: {preview.IncompleteCount} · Omitidos: {preview.OmittedCount}";

            StatusMessage = "Archivo listo para generar.";
            OnPropertyChanged(nameof(CanGenerate));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La carga fue cancelada.";
        }
        catch (Exception exception)
        {
            PreviewRecords.Clear();
            ImportSummaryText = "No fue posible leer el archivo.";
            StatusMessage = $"Error al leer la hoja seleccionada: {exception.Message}";
            OnPropertyChanged(nameof(CanGenerate));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateFilesAsync()
    {
        if (!_selectedEvaluationType.HasValue)
        {
            StatusMessage = "Selecciona un tipo de evaluación antes de generar.";
            return;
        }

        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            StatusMessage = "No hay un directorio base configurado.";
            return;
        }

        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "FICHA MEDICA.xlsx");
        if (!File.Exists(templatePath))
        {
            StatusMessage = $"No se encontró la plantilla en {templatePath}.";
            return;
        }

        var selectedRecords = PreviewRecords
            .Where(static row => row.CanGenerate && row.IsSelected)
            .Select(static row => row.ToSelection())
            .ToList();

        if (selectedRecords.Count == 0)
        {
            StatusMessage = "No hay registros válidos para generar.";
            return;
        }

        _generationCancellationTokenSource?.Cancel();
        _generationCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsGenerating = true;
            StatusMessage = "Generando fichas médicas individuales...";
            GenerationSummaryText = string.Empty;

            var summary = await _generationService.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = templatePath,
                    OutputBaseDirectory = baseDirectory,
                    EvaluationType = _selectedEvaluationType.Value,
                    GeneratedAt = _dateService.Today,
                    Records = selectedRecords
                },
                _generationCancellationTokenSource.Token);

            _lastGeneratedOutputPath = summary.OutputRootPath;
            OnPropertyChanged(nameof(CanOpenOutputFolder));

            GenerationSummaryText = $"Generados: {summary.SucceededCount} · Fallidos/Omitidos: {summary.FailedCount}";
            StatusMessage = summary.FailedCount == 0
                ? "Generación completada correctamente."
                : "Generación completada con advertencias o errores parciales.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La generación fue cancelada por el usuario.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Error durante la generación: {exception.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private void CancelGeneration()
    {
        _generationCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Cancel();
        StatusMessage = "Cancelación solicitada.";
    }

    private async Task OpenOutputFolderAsync()
    {
        if (!CanOpenOutputFolder || string.IsNullOrWhiteSpace(_lastGeneratedOutputPath))
        {
            return;
        }

        try
        {
            await _folderLauncherService.OpenFolderAsync(_lastGeneratedOutputPath);
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible abrir la carpeta generada: {exception.Message}";
        }
    }

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

    private void SelectEvaluationType(OccupationalEvaluationType evaluationType)
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
        OnPropertyChanged(nameof(CanGenerate));
        StatusMessage = $"Tipo de evaluación seleccionado: {MedicalGenerationOptions.GetVisibleFolderName(evaluationType)}.";
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

    private Color GetEvaluationBackground(OccupationalEvaluationType evaluationType)
    {
        return _selectedEvaluationType == evaluationType ? SelectedEvaluationBackground : IdleEvaluationBackground;
    }

    private Color GetEvaluationTextColor(OccupationalEvaluationType evaluationType)
    {
        return _selectedEvaluationType == evaluationType ? SelectedText : IdleEvaluationText;
    }
}
