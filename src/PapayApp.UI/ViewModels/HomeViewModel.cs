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
    private readonly IFolderLauncherService _folderLauncherService;
    private readonly IExcelImportService _excelImportService;
    private readonly IMedicalEvaluationGenerationService _generationService;
    private MainSection _selectedSection = MainSection.Home;
    private OccupationalEvaluationType? _selectedEvaluationType;
    private string? _selectedUploadFileName;
    private string? _masterFilePath;
    private string? _selectedWorksheet;
    private bool _isBusy;
    private bool _isGenerating;
    private string _statusMessage = string.Empty;
    private string _dateText = string.Empty;
    private string _timeText = string.Empty;
    private string _importSummaryText = "Selecciona un archivo maestro para comenzar.";
    private string _generationSummaryText = string.Empty;
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
        IBaseDirectoryService baseDirectoryService,
        IFolderLauncherService folderLauncherService,
        IExcelImportService excelImportService,
        IMedicalEvaluationGenerationService generationService)
    {
        _dateService = dateService;
        _fileSelectionService = fileSelectionService;
        _baseDirectoryService = baseDirectoryService;
        _folderLauncherService = folderLauncherService;
        _excelImportService = excelImportService;
        _generationService = generationService;

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

    public ObservableCollection<DirectoryEntryViewModel> DirectoryEntries { get; }

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
            SelectedWorksheet = ResolvePreferredWorksheetName(AvailableWorksheets);
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
                $"Hoja: {preview.WorksheetName} · Validos: {preview.ValidCount} · Incompletos: {preview.IncompleteCount} · Omitidos: {preview.OmittedCount}";

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
            StatusMessage = "Selecciona un tipo de evaluacion antes de generar.";
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
            StatusMessage = $"No se encontro la plantilla en {templatePath}.";
            return;
        }

        var selectedRecords = PreviewRecords
            .Where(static row => row.CanGenerate && row.IsSelected)
            .Select(static row => row.ToSelection())
            .ToList();

        if (selectedRecords.Count == 0)
        {
            StatusMessage = "No hay registros validos para generar.";
            return;
        }

        _generationCancellationTokenSource?.Cancel();
        _generationCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsGenerating = true;
            StatusMessage = "Generando fichas medicas individuales...";
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

            GenerationSummaryText = $"Generados: {summary.SucceededCount} · Fallidos/Omitidos: {summary.FailedCount}";
            StatusMessage = summary.FailedCount == 0
                ? "Generacion completada correctamente."
                : "Generacion completada con advertencias o errores parciales.";

            GenerationCompleted?.Invoke(
                this,
                summary.FailedCount == 0
                    ? $"Se generaron {summary.SucceededCount} archivos correctamente."
                    : $"Se generaron {summary.SucceededCount} archivos. Revisa el resumen: {summary.FailedCount} quedaron con advertencias o errores.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La generacion fue cancelada por el usuario.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Error durante la generacion: {exception.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task RefreshDirectoriesAsync()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            DirectoryEntries.Clear();
            FoldersCurrentPathText = "Ruta actual: -";
            FoldersStatusMessage = "No hay un directorio base configurado.";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
            return;
        }

        var targetDirectory = _foldersCurrentDirectoryPath;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            targetDirectory = baseDirectory;
        }

        await LoadDirectoryEntriesAsync(targetDirectory);
    }

    private async Task NavigateUpDirectoryAsync()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(_foldersCurrentDirectoryPath))
        {
            return;
        }

        var currentDirectory = NormalizeDirectoryPath(_foldersCurrentDirectoryPath);
        var rootDirectory = NormalizeDirectoryPath(baseDirectory);
        if (string.Equals(currentDirectory, rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return;
        }

        await LoadDirectoryEntriesAsync(parentDirectory);
    }

    private async Task OpenCurrentDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_foldersCurrentDirectoryPath))
        {
            return;
        }

        try
        {
            await _folderLauncherService.OpenFolderAsync(_foldersCurrentDirectoryPath);
        }
        catch (Exception exception)
        {
            FoldersStatusMessage = $"No fue posible abrir la carpeta actual: {exception.Message}";
        }
    }

    private async Task ChangeBaseDirectoryAsync()
    {
        try
        {
            var selectedPath = await _baseDirectoryService.ChangeBaseDirectoryAsync();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            _foldersCurrentDirectoryPath = selectedPath;
            RefreshSettingsState();
            SettingsStatusMessage = "Ruta base actualizada correctamente.";
            await RefreshDirectoriesAsync();
        }
        catch (Exception exception)
        {
            SettingsStatusMessage = $"No fue posible cambiar la ruta base: {exception.Message}";
        }
    }

    private async Task LoadDirectoryEntriesAsync(string directoryPath)
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        var normalizedBaseDirectory = NormalizeDirectoryPath(baseDirectory);
        var normalizedTargetDirectory = NormalizeDirectoryPath(directoryPath);
        if (!IsWithinBaseDirectory(normalizedBaseDirectory, normalizedTargetDirectory))
        {
            normalizedTargetDirectory = normalizedBaseDirectory;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(normalizedTargetDirectory);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException("La carpeta solicitada no existe.");
            }

            var directoryEntries = directoryInfo
                .EnumerateDirectories()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(directory => CreateDirectoryEntry(directory, normalizedBaseDirectory))
                .Concat(
                    directoryInfo
                        .EnumerateFiles()
                        .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(file => CreateFileEntry(file, normalizedBaseDirectory)))
                .ToList();

            DirectoryEntries.Clear();
            foreach (var entry in directoryEntries)
            {
                DirectoryEntries.Add(entry);
            }

            _foldersCurrentDirectoryPath = normalizedTargetDirectory;
            FoldersCurrentPathText = $"Ruta actual: {BuildRelativeDirectoryPath(normalizedBaseDirectory, normalizedTargetDirectory)}";
            FoldersStatusMessage = directoryEntries.Count == 0
                ? "Esta carpeta aun no contiene archivos ni subcarpetas."
                : $"{directoryEntries.Count} elementos en la carpeta actual.";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
        }
        catch (Exception exception)
        {
            DirectoryEntries.Clear();
            FoldersStatusMessage = $"No fue posible leer la carpeta: {exception.Message}";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
        }

        await Task.CompletedTask;
    }

    private DirectoryEntryViewModel CreateDirectoryEntry(DirectoryInfo directoryInfo, string baseDirectory)
    {
        return new DirectoryEntryViewModel
        {
            Name = directoryInfo.Name,
            RelativePath = BuildRelativeEntryPath(baseDirectory, directoryInfo.FullName),
            ItemTypeText = "Carpeta",
            ModifiedText = $"Actualizada: {directoryInfo.LastWriteTime:dd/MM/yyyy HH:mm}",
            IconSource = "folder_directory.png",
            IsDirectory = true,
            OpenCommand = new AsyncRelayCommand(() => LoadDirectoryEntriesAsync(directoryInfo.FullName))
        };
    }

    private DirectoryEntryViewModel CreateFileEntry(FileInfo fileInfo, string baseDirectory)
    {
        return new DirectoryEntryViewModel
        {
            Name = fileInfo.Name,
            RelativePath = BuildRelativeEntryPath(baseDirectory, fileInfo.FullName),
            ItemTypeText = "Archivo",
            ModifiedText = $"Actualizado: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}",
            IconSource = "excel_file.png",
            IsDirectory = false,
            OpenCommand = new AsyncRelayCommand(() => OpenFileAsync(fileInfo.FullName))
        };
    }

    private async Task OpenFileAsync(string filePath)
    {
        try
        {
            await _folderLauncherService.OpenPathAsync(filePath);
        }
        catch (Exception exception)
        {
            FoldersStatusMessage = $"No fue posible abrir el archivo: {exception.Message}";
        }
    }

    private static string BuildRelativeDirectoryPath(string baseDirectory, string currentDirectory)
    {
        var relativePath = Path.GetRelativePath(baseDirectory, currentDirectory);
        return relativePath == "." ? "Base" : relativePath;
    }

    private static string BuildRelativeEntryPath(string baseDirectory, string entryPath)
    {
        return Path.GetRelativePath(baseDirectory, entryPath);
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsWithinBaseDirectory(string baseDirectory, string targetDirectory)
    {
        if (string.Equals(baseDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedBaseDirectory = $"{baseDirectory}{Path.DirectorySeparatorChar}";
        var normalizedTargetDirectory = $"{targetDirectory}{Path.DirectorySeparatorChar}";
        return normalizedTargetDirectory.StartsWith(normalizedBaseDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSettingsState()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        SettingsBaseDirectoryPathText = string.IsNullOrWhiteSpace(baseDirectory)
            ? "No hay una ruta base configurada."
            : baseDirectory;
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

        if (section == MainSection.Settings)
        {
            RefreshSettingsState();
        }
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
        SelectPreferredWorksheetIfAvailable();
        StatusMessage = $"Tipo de evaluacion seleccionado: {MedicalGenerationOptions.GetVisibleFolderName(evaluationType)}.";
    }

    private void SelectPreferredWorksheetIfAvailable()
    {
        if (AvailableWorksheets.Count == 0)
        {
            return;
        }

        var preferredWorksheet = ResolvePreferredWorksheetName(AvailableWorksheets);
        if (!string.IsNullOrWhiteSpace(preferredWorksheet))
        {
            SelectedWorksheet = preferredWorksheet;
        }
    }

    private string? ResolvePreferredWorksheetName(IEnumerable<string> worksheetNames)
    {
        var worksheets = worksheetNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (worksheets.Count == 0)
        {
            return null;
        }

        var preferredNames = _selectedEvaluationType == OccupationalEvaluationType.Retiro
            ? new[] { "RETIRO", "RETIROS" }
            : new[] { "INGRESO", "INGRESOS" };

        foreach (var preferredName in preferredNames)
        {
            var exactMatch = worksheets.FirstOrDefault(name =>
                string.Equals(name.Trim(), preferredName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }
        }

        foreach (var preferredName in preferredNames)
        {
            var containedMatch = worksheets.FirstOrDefault(name =>
                name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(containedMatch))
            {
                return containedMatch;
            }
        }

        return worksheets.FirstOrDefault();
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
