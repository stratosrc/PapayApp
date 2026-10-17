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
        IBaseDirectoryService baseDirectoryService)
    {
        _importService = importService;
        _generationService = generationService;
        _fileSelectionService = fileSelectionService;
        _folderPickerService = folderPickerService;
        _dialogService = dialogService;
        _folioStore = folioStore;
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
    }

    public string Title => "Certificados";

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
        !string.IsNullOrWhiteSpace(CertificateType) &&
        !string.IsNullOrWhiteSpace(BodyTemplate) &&
        !string.IsNullOrWhiteSpace(SignerName) &&
        !string.IsNullOrWhiteSpace(SignerTitle) &&
        HasSignature &&
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
            ImportStatus = exception.Message;
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
            .Matches(BodyTemplate)
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

        if (!BodyTemplate.Contains("{cedula}", StringComparison.OrdinalIgnoreCase))
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

        var request = new CertificateGenerationRequest
        {
            TemplatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "CERTIFICADO.pdf"),
            AlexBrushFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "AlexBrush-Regular.ttf"),
            CinzelRegularFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Cinzel-Regular.ttf"),
            CinzelBoldFontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Cinzel-Bold.ttf"),
            OutputDirectory = OutputDirectory,
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
        }
        catch (Exception exception)
        {
            LastUsedFolioText = reservedLastUsedFolio.ToString();
            GenerationStatus =
                $"No se pudieron generar los certificados: {exception.Message}" + Environment.NewLine +
                $"Los folios {lastUsedFolio + 1}-{reservedLastUsedFolio} permanecen utilizados.";
        }
        finally
        {
            IsBusy = false;
            NotifyGenerationStateChanged();
        }
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
