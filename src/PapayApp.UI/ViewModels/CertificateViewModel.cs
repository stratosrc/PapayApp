using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed class CertificateViewModel : BaseViewModel
{
    private static readonly Regex VariableRegex = new(@"\{[^{}]+\}", RegexOptions.Compiled);
    private static readonly Color SelectedBackground = Color.FromArgb("#245779");
    private static readonly Color IdleBackground = Color.FromArgb("#223440");
    private static readonly Color SelectedText = Colors.White;
    private static readonly Color IdleText = Color.FromArgb("#F0F5F9");

    private readonly ICertificateImportService _importService;
    private readonly ICertificateGenerationService _generationService;
    private readonly IFileSelectionService _fileSelectionService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IAppDialogService _dialogService;
    private readonly ICertificateFolioStore _folioStore;
    private readonly ICertificateTemplateStore _templateStore;
    private bool _isExcelMode = true;
    private bool _isCombinedOutput = true;
    private bool _isBusy;
    private string _excelFileName = "Ningún Excel seleccionado.";
    private string _importStatus = "Carga un .xlsx con nombres en la columna A y cédulas en la columna B.";
    private string _certificateType = string.Empty;
    private string _bodyTemplate = string.Empty;
    private DateTime _certificateDate = DateTime.Today;
    private string _lastUsedFolioText;
    private string _manualFullName = string.Empty;
    private string _manualIdentification = string.Empty;
    private string _signerName = string.Empty;
    private string _signerTitle = string.Empty;
    private string? _signaturePath;
    private string _signatureFileName = "Ninguna firma seleccionada.";
    private string _outputDirectory;
    private string _generationStatus = string.Empty;

    public CertificateViewModel(
        ICertificateImportService importService,
        ICertificateGenerationService generationService,
        IFileSelectionService fileSelectionService,
        IFolderPickerService folderPickerService,
        IAppDialogService dialogService,
        ICertificateFolioStore folioStore,
        IBaseDirectoryService baseDirectoryService,
        ICertificateTemplateStore templateStore,
        IFolderLauncherService launcher)
    {
        _importService = importService;
        _generationService = generationService;
        _fileSelectionService = fileSelectionService;
        _folderPickerService = folderPickerService;
        _dialogService = dialogService;
        _folioStore = folioStore;
        _templateStore = templateStore;
        Results = new(launcher, () => { ClearParticipants(); ManualFullName = ""; ManualIdentification = ""; GenerationStatus = "Nuevo lote: carga otro Excel o introduce una persona. Los datos comunes se conservan."; NotifyParticipantsChanged(); });
        _lastUsedFolioText = folioStore.LastUsedFolio.ToString();
        _outputDirectory = string.IsNullOrWhiteSpace(baseDirectoryService.BaseDirectoryPath)
            ? string.Empty
            : Path.Combine(baseDirectoryService.BaseDirectoryPath, "CERTIFICADOS");

        Participants = new ObservableCollection<CertificateParticipantRowViewModel>();

        SelectExcelModeCommand = new RelayCommand(() => IsExcelMode = true);
        SelectManualModeCommand = new RelayCommand(() => IsExcelMode = false);
        SelectCombinedOutputCommand = new RelayCommand(() => IsCombinedOutput = true);
        SelectSeparateOutputCommand = new RelayCommand(() => IsCombinedOutput = false);
        PickExcelCommand = new AsyncRelayCommand(PickExcelAsync);
        PickSignatureCommand = new AsyncRelayCommand(PickSignatureAsync);
        PickOutputDirectoryCommand = new AsyncRelayCommand(PickOutputDirectoryAsync);
        SelectAllCommand = new RelayCommand(() => SetAllSelections(true));
        ClearSelectionCommand = new RelayCommand(() => SetAllSelections(false));
        InsertIdentificationTokenCommand = new RelayCommand(InsertIdentificationToken);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        RefreshTemplate();
    }

    public string Title => "Certificados";

    public ResultActionsViewModel Results { get; }

    public string ActiveTemplateName
    {
        get { try { return "Plantilla activa: " + _templateStore.Load().Name; } catch { return "Plantilla no disponible. Revisa Administrar plantillas."; } }
    }

    public ObservableCollection<CertificateInputViewModel> GenerationFields { get; } = [];
    private readonly Dictionary<string, Dictionary<string, string>> _templateInputValues = [];
    private string _inputTemplateId = string.Empty;
    public bool HasGenerationFields => GenerationFields.Count > 0;
    public bool UsesSignerName { get; private set; } = true;
    public bool UsesSignerTitle { get; private set; } = true;
    public bool UsesMainSignature { get; private set; } = true;
    public bool HasMainSigner => UsesSignerName || UsesSignerTitle || UsesMainSignature;

    public void RefreshTemplate()
    {
        OnPropertyChanged(nameof(ActiveTemplateName));
        try
        {
            var template = _templateStore.Load();
            if (!string.IsNullOrEmpty(_inputTemplateId))
                _templateInputValues[_inputTemplateId] = GenerationFields.ToDictionary(f => f.Key, f => f.Value);
            _inputTemplateId = template.Id;
            _templateInputValues.TryGetValue(_inputTemplateId, out var previous);
            GenerationFields.Clear();
            foreach (var element in template.Layout.GenerationInputs)
            {
                var field = new CertificateInputViewModel(element, _fileSelectionService);
                if (previous is not null && previous.TryGetValue(field.Key, out var value)) field.Value = value;
                GenerationFields.Add(field);
            }
            UsesSignerName = template.Layout.Uses("{firmante}");
            UsesSignerTitle = template.Layout.Uses("{cargo}");
            UsesMainSignature = template.Layout.RequiresSignature;
            foreach (var p in new[] { nameof(HasGenerationFields), nameof(UsesSignerName), nameof(UsesSignerTitle), nameof(UsesMainSignature), nameof(HasMainSigner) }) OnPropertyChanged(p);
        }
        catch (Exception e) { GenerationStatus = UserProblem.Describe(e, "cargar los campos de la plantilla"); }
    }

    public string ExcelRulesText =>
        "Reglas: fila 1 con encabezados; columna A = Apellidos y Nombres; " +
        "columna B = Número de cédula; datos desde la fila 2; cédulas de 10 dígitos.";

    public ObservableCollection<CertificateParticipantRowViewModel> Participants { get; }

    public ICommand SelectExcelModeCommand { get; }

    public ICommand SelectManualModeCommand { get; }

    public ICommand SelectCombinedOutputCommand { get; }

    public ICommand SelectSeparateOutputCommand { get; }

    public ICommand PickExcelCommand { get; }

    public ICommand PickSignatureCommand { get; }

    public ICommand PickOutputDirectoryCommand { get; }

    public ICommand SelectAllCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public ICommand InsertIdentificationTokenCommand { get; }

    public ICommand GenerateCommand { get; }

    public bool IsExcelMode
    {
        get => _isExcelMode;
        set
        {
            if (SetProperty(ref _isExcelMode, value))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(ExcelModeBackground));
                OnPropertyChanged(nameof(ExcelModeTextColor));
                OnPropertyChanged(nameof(ManualModeBackground));
                OnPropertyChanged(nameof(ManualModeTextColor));
                NotifyGenerationStateChanged();
            }
        }
    }

    public bool IsManualMode => !IsExcelMode;

    public Color ExcelModeBackground => IsExcelMode ? SelectedBackground : IdleBackground;

    public Color ExcelModeTextColor => IsExcelMode ? SelectedText : IdleText;

    public Color ManualModeBackground => IsManualMode ? SelectedBackground : IdleBackground;

    public Color ManualModeTextColor => IsManualMode ? SelectedText : IdleText;

    public bool IsCombinedOutput
    {
        get => _isCombinedOutput;
        set
        {
            if (SetProperty(ref _isCombinedOutput, value))
            {
                OnPropertyChanged(nameof(IsSeparateOutput));
                OnPropertyChanged(nameof(CombinedOutputBackground));
                OnPropertyChanged(nameof(CombinedOutputTextColor));
                OnPropertyChanged(nameof(SeparateOutputBackground));
                OnPropertyChanged(nameof(SeparateOutputTextColor));
            }
        }
    }

    public bool IsSeparateOutput => !IsCombinedOutput;

    public Color CombinedOutputBackground => IsCombinedOutput ? SelectedBackground : IdleBackground;

    public Color CombinedOutputTextColor => IsCombinedOutput ? SelectedText : IdleText;

    public Color SeparateOutputBackground => IsSeparateOutput ? SelectedBackground : IdleBackground;

    public Color SeparateOutputTextColor => IsSeparateOutput ? SelectedText : IdleText;

    public string ExcelFileName
    {
        get => _excelFileName;
        private set => SetProperty(ref _excelFileName, value);
    }

    public string ImportStatus
    {
        get => _importStatus;
        private set => SetProperty(ref _importStatus, value);
    }

    public bool HasParticipants => Participants.Count > 0;

    public string ParticipantsCountText =>
        $"Seleccionados: {SelectedParticipantsCount} de {Participants.Count}";

    public string CertificateType
    {
        get => _certificateType;
        set
        {
            var uppercaseValue = value?.ToUpperInvariant() ?? string.Empty;
            if (SetProperty(ref _certificateType, uppercaseValue))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string BodyTemplate
    {
        get => _bodyTemplate;
        set
        {
            if (SetProperty(ref _bodyTemplate, value))
            {
                OnPropertyChanged(nameof(BodyPreviewText));
                NotifyGenerationStateChanged();
            }
        }
    }

    public string BodyPreviewText => string.IsNullOrWhiteSpace(BodyTemplate)
        ? "Vista previa del texto: -"
        : $"Vista previa: {BodyTemplate.Replace("{cedula}", "0953456423", StringComparison.OrdinalIgnoreCase)}";

    public DateTime CertificateDate
    {
        get => _certificateDate;
        set => SetProperty(ref _certificateDate, value);
    }

    public string LastUsedFolioText
    {
        get => _lastUsedFolioText;
        set
        {
            if (SetProperty(ref _lastUsedFolioText, value))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string FolioRangeText
    {
        get
        {
            if (!int.TryParse(LastUsedFolioText, out var lastUsed) || lastUsed < 0)
            {
                return "Introduce un último folio válido.";
            }

            var count = SelectedParticipantsCount;
            if (count == 0)
            {
                return $"Próximo folio: {lastUsed + 1}";
            }

            return $"Se asignarán {lastUsed + 1}-{lastUsed + count}. " +
                   $"El siguiente será {lastUsed + count + 1}.";
        }
    }

    public string ManualFullName
    {
        get => _manualFullName;
        set
        {
            if (SetProperty(ref _manualFullName, value))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string ManualIdentification
    {
        get => _manualIdentification;
        set
        {
            if (SetProperty(ref _manualIdentification, value))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string SignerName
    {
        get => _signerName;
        set
        {
            if (SetProperty(ref _signerName, value))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string SignerTitle
    {
        get => _signerTitle;
        set
        {
            if (SetProperty(ref _signerTitle, value))
            {
                NotifyGenerationStateChanged();
            }
        }
    }

    public string? SignaturePath
    {
        get => _signaturePath;
        private set
        {
            if (SetProperty(ref _signaturePath, value))
            {
                OnPropertyChanged(nameof(HasSignature));
                NotifyGenerationStateChanged();
            }
        }
    }

    public bool HasSignature => !string.IsNullOrWhiteSpace(SignaturePath);

    public string SignatureFileName
    {
        get => _signatureFileName;
        private set => SetProperty(ref _signatureFileName, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        private set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                OnPropertyChanged(nameof(OutputDirectoryText));
                NotifyGenerationStateChanged();
            }
        }
    }

    public string OutputDirectoryText => string.IsNullOrWhiteSpace(OutputDirectory)
        ? "Ninguna carpeta seleccionada."
        : OutputDirectory;

    public string GenerationStatus
    {
        get => _generationStatus;
        private set
        {
            if (SetProperty(ref _generationStatus, value))
            {
                OnPropertyChanged(nameof(HasGenerationStatus));
            }
        }
    }

    public bool HasGenerationStatus => !string.IsNullOrWhiteSpace(GenerationStatus);

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

    public bool CanGenerate =>
        !IsBusy &&
        SelectedParticipantsCount > 0 &&
        int.TryParse(LastUsedFolioText, out var folio) &&
        folio >= 0 &&
        !string.IsNullOrWhiteSpace(OutputDirectory) &&
        (!IsManualMode || IsManualParticipantValid());

    private int SelectedParticipantsCount => IsManualMode
        ? (IsManualParticipantValid() ? 1 : 0)
        : Participants.Count(static participant => participant.IsSelected);

    private async Task PickExcelAsync()
    {
        var file = await _fileSelectionService.PickSingleFileAsync(
            "Selecciona el registro de asistencia",
            new[] { ".xlsx" });
        if (file is null)
        {
            return;
        }

        IsBusy = true;
        ImportStatus = "Validando la estructura del Excel...";

        try
        {
            var result = await _importService.ImportAsync(file.FullPath);
            ClearParticipants();

            if (!result.IsValid)
            {
                ExcelFileName = "Excel inválido.";
                ImportStatus = string.Join(Environment.NewLine, result.Errors.Prepend(ExcelRulesText));
                return;
            }

            ExcelFileName = file.FileName;
            foreach (var participant in result.Participants)
            {
                var row = new CertificateParticipantRowViewModel(participant);
                row.PropertyChanged += OnParticipantPropertyChanged;
                Participants.Add(row);
            }

            var lines = new List<string>
            {
                $"Excel válido: {Participants.Count} personas en la hoja '{result.WorksheetName}'."
            };
            lines.AddRange(result.Warnings);
            ImportStatus = string.Join(Environment.NewLine, lines);
        }
        catch (Exception exception)
        {
            ExcelFileName = "No se pudo cargar el Excel.";
            ImportStatus = UserProblem.Describe(exception, "leer el Excel");
        }
        finally
        {
            IsBusy = false;
            NotifyParticipantsChanged();
        }
    }

    private async Task PickSignatureAsync()
    {
        var file = await _fileSelectionService.PickSingleFileAsync(
            "Selecciona la firma en formato PNG",
            new[] { ".png" });
        if (file is null)
        {
            return;
        }

        SignaturePath = file.FullPath;
        SignatureFileName = file.FileName;
    }

    private async Task PickOutputDirectoryAsync()
    {
        var selectedPath = await _folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        OutputDirectory = selectedPath;
    }

    private async Task GenerateAsync()
    {
        CertificateTemplateSettings template;
        try { template = _templateStore.Load(); }
        catch (Exception exception)
        {
            GenerationStatus = $"No se pudo cargar la plantilla: {exception.Message}. Revisa Ajustes.";
            return;
        }
        var values = GenerationFields.ToDictionary(f => f.Key, f => f.Value);
        var missingInputs = template.Layout.GenerationInputs.Where(e =>
            !values.TryGetValue(e.InputKey, out var value) || string.IsNullOrWhiteSpace(value)).Select(e => e.Name).ToList();
        if (missingInputs.Count > 0)
        {
            GenerationStatus = "Completa los datos de esta plantilla: " + string.Join(", ", missingInputs) + ". Los encontrarás en Datos adicionales de la plantilla.";
            return;
        }
        foreach (var signature in template.Layout.GenerationInputs.Where(e => e.Kind == CertificateElementKind.Signature))
        {
            var path = values[signature.InputKey];
            if (!File.Exists(path) || !Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                GenerationStatus = $"Vuelve a seleccionar el archivo PNG de '{signature.Name}' en Datos adicionales de la plantilla. El archivo no está disponible o su formato no es PNG.";
                return;
            }
        }

        if ((template.Layout.Uses("{tipo}") && string.IsNullOrWhiteSpace(CertificateType)) ||
            (template.Layout.Uses("{texto}") && string.IsNullOrWhiteSpace(BodyTemplate)) ||
            (template.Layout.Uses("{firmante}") && string.IsNullOrWhiteSpace(SignerName)) ||
            (template.Layout.Uses("{cargo}") && string.IsNullOrWhiteSpace(SignerTitle)) ||
            (template.Layout.RequiresSignature && !HasSignature))
        {
            var missing = new List<string>();
            if (template.Layout.Uses("{tipo}") && string.IsNullOrWhiteSpace(CertificateType)) missing.Add("Tipo de certificado");
            if (template.Layout.Uses("{texto}") && string.IsNullOrWhiteSpace(BodyTemplate)) missing.Add("Texto común");
            if (template.Layout.Uses("{firmante}") && string.IsNullOrWhiteSpace(SignerName)) missing.Add("Nombre del firmante");
            if (template.Layout.Uses("{cargo}") && string.IsNullOrWhiteSpace(SignerTitle)) missing.Add("Título o cargo");
            if (template.Layout.RequiresSignature && !HasSignature) missing.Add("Firma PNG");
            GenerationStatus = "Faltan datos: " + string.Join(", ", missing) + ". Completa esos campos y vuelve a generar.";
            return;
        }
        if (!TryBuildParticipants(out var participants, out var validationMessage))
        {
            GenerationStatus = validationMessage;
            return;
        }

        if (!int.TryParse(LastUsedFolioText, out var lastUsedFolio) || lastUsedFolio < 0)
        {
            GenerationStatus = "El último folio utilizado debe ser un número entero positivo o cero.";
            return;
        }

        var unsupportedVariables = VariableRegex
            .Matches(template.Layout.Uses("{texto}") ? BodyTemplate : string.Empty)
            .Select(static match => match.Value)
            .Where(static variable => !variable.Equals("{cedula}", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unsupportedVariables.Count > 0)
        {
            GenerationStatus =
                $"Variables no reconocidas: {string.Join(", ", unsupportedVariables)}. Solo puedes usar {{cedula}}.";
            return;
        }

        if (lastUsedFolio < _folioStore.LastUsedFolio)
        {
            var continueWithPrevious = await _dialogService.ConfirmAsync(
                "Folio anterior",
                $"El último folio guardado es {_folioStore.LastUsedFolio}. " +
                $"Has introducido {lastUsedFolio}. Si continúas, la secuencia se reiniciará desde {lastUsedFolio + 1}.",
                "Continuar",
                "Cancelar");
            if (!continueWithPrevious)
            {
                return;
            }
        }

        if (template.Layout.Uses("{texto}") && !template.Layout.Uses("{cedula}") &&
            !BodyTemplate.Contains("{cedula}", StringComparison.OrdinalIgnoreCase))
        {
            var continueWithoutIdentification = await _dialogService.ConfirmAsync(
                "Texto sin cédula",
                "El texto no contiene la variable {cedula}. Los certificados se generarán sin insertar la cédula. ¿Deseas continuar?",
                "Continuar",
                "Cancelar");
            if (!continueWithoutIdentification)
            {
                return;
            }
        }

        int reservedLastUsedFolio;
        try
        {
            reservedLastUsedFolio = checked(lastUsedFolio + participants.Count);
        }
        catch (OverflowException)
        {
            GenerationStatus = "El rango de folios supera el valor máximo permitido.";
            return;
        }

        var request = BuildRequest(template, participants, lastUsedFolio, OutputDirectory);

        Results.Clear();
        IsBusy = true;
        GenerationStatus = "Preparando certificados...";
        _folioStore.LastUsedFolio = reservedLastUsedFolio;

        try
        {
            var progress = new Progress<CertificateGenerationProgressInfo>(info =>
            {
                GenerationStatus =
                    $"Generando {info.Processed} de {info.Total}: folio {info.Folio}, {info.ParticipantName}.";
            });

            var result = await _generationService.GenerateAsync(request, progress);
            Results.Set(result.Results.FirstOrDefault(r => r.Success)?.OutputPath, OutputDirectory, result.LogPath);
            if (result.SucceededCount > 0) RecentWorkStore.Add($"Certificados: {result.SucceededCount} personas", OutputDirectory);
            _folioStore.LastUsedFolio = result.LastUsedFolio;
            LastUsedFolioText = result.LastUsedFolio.ToString();

            var summaryLines = new List<string>
            {
                $"Generados: {result.SucceededCount}",
                $"Fallidos: {result.FailedCount}",
                $"Último folio utilizado: {result.LastUsedFolio}",
                $"Siguiente folio: {result.NextFolio}"
            };

            if (!string.IsNullOrWhiteSpace(result.CombinedPdfPath))
            {
                summaryLines.Add($"PDF: {result.CombinedPdfPath}");
            }

            if (!string.IsNullOrWhiteSpace(result.LogPath))
            {
                summaryLines.Add($"Log de fallos: {result.LogPath}");
            }

            var failedFolios = result.Results
                .Where(static item => !item.Success)
                .Select(static item => item.Folio)
                .ToList();
            if (failedFolios.Count > 0)
            {
                summaryLines.Add($"Folios fallidos: {string.Join(", ", failedFolios)}");
            }

            GenerationStatus = string.Join(Environment.NewLine, summaryLines);
            if (result.FailedCount > 0)
                GenerationStatus += "\nRevisa: " + result.Results.First(r => !r.Success).ErrorMessage +
                    "\nPulsa Ver errores para identificar las personas afectadas. Si el texto no cabe, amplía su área en Administrar plantillas y guarda.";
        }
        catch (Exception exception)
        {
            LastUsedFolioText = reservedLastUsedFolio.ToString();
            GenerationStatus =
                UserProblem.Describe(exception, "generar los certificados") + Environment.NewLine +
                $"Los folios {lastUsedFolio + 1}-{reservedLastUsedFolio} permanecen utilizados.";
        }
        finally
        {
            IsBusy = false;
            NotifyGenerationStateChanged();
        }
    }

    private CertificateGenerationRequest BuildRequest(CertificateTemplateSettings template, IReadOnlyList<CertificateParticipant> participants, int lastUsedFolio, string outputDirectory)
    {
        return new CertificateGenerationRequest
        {
            TemplatePath = template.TemplatePath,
            Layout = template.Layout,
            FieldValues = GenerationFields.ToDictionary(f => f.Key, f => f.Value),
            AlexBrushFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "AlexBrush-Regular.ttf"),
            CinzelRegularFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Cinzel-Regular.ttf"),
            CinzelBoldFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Cinzel-Bold.ttf"),
            OutputDirectory = outputDirectory,
            SignaturePngPath = SignaturePath!,
            CertificateType = CertificateType,
            BodyTemplate = BodyTemplate,
            SignerName = SignerName,
            SignerTitle = SignerTitle,
            Participants = participants,
            OutputMode = IsCombinedOutput
                ? CertificateOutputMode.CombinedPdf
                : CertificateOutputMode.SeparateFiles,
            CertificateDate = CertificateDate,
            LastUsedFolio = lastUsedFolio
        };

    }

    private bool TryBuildParticipants(
        out IReadOnlyList<CertificateParticipant> participants,
        out string validationMessage)
    {
        if (IsExcelMode)
        {
            participants = Participants
                .Where(static participant => participant.IsSelected)
                .Select(static participant => participant.Participant)
                .ToList();

            validationMessage = participants.Count == 0
                ? "Carga un Excel válido y selecciona al menos una persona."
                : string.Empty;
            return participants.Count > 0;
        }

        if (!IsManualParticipantValid())
        {
            participants = Array.Empty<CertificateParticipant>();
            validationMessage =
                "Para la generación manual introduce el nombre completo y una cédula de exactamente 10 dígitos.";
            return false;
        }

        ManualFullName = PersonNameFormatter.ToTitleCase(ManualFullName);
        participants = new[]
        {
            new CertificateParticipant(0, ManualFullName, ManualIdentification.Trim())
        };
        validationMessage = string.Empty;
        return true;
    }

    private bool IsManualParticipantValid()
    {
        var identification = ManualIdentification.Trim();
        return !string.IsNullOrWhiteSpace(ManualFullName) &&
               identification.Length == 10 &&
               identification.All(static character => char.IsDigit(character));
    }

    private void InsertIdentificationToken()
    {
        if (string.IsNullOrWhiteSpace(BodyTemplate))
        {
            BodyTemplate = "{cedula}";
            return;
        }

        BodyTemplate = $"{BodyTemplate.TrimEnd()} {{cedula}}";
    }

    private void SetAllSelections(bool selected)
    {
        foreach (var participant in Participants)
        {
            participant.IsSelected = selected;
        }

        NotifyParticipantsChanged();
    }

    private void ClearParticipants()
    {
        foreach (var participant in Participants)
        {
            participant.PropertyChanged -= OnParticipantPropertyChanged;
        }

        Participants.Clear();
    }

    private void OnParticipantPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(CertificateParticipantRowViewModel.IsSelected))
        {
            NotifyParticipantsChanged();
        }
    }

    private void NotifyParticipantsChanged()
    {
        OnPropertyChanged(nameof(HasParticipants));
        OnPropertyChanged(nameof(ParticipantsCountText));
        NotifyGenerationStateChanged();
    }

    private void NotifyGenerationStateChanged()
    {
        OnPropertyChanged(nameof(CanGenerate));
        OnPropertyChanged(nameof(FolioRangeText));
        OnPropertyChanged(nameof(ParticipantsCountText));
    }
}
