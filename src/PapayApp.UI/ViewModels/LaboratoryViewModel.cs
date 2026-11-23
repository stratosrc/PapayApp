using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;

public sealed class LaboratoryPatientRow : BaseViewModel
{
    public LaboratoryPatientRow(LaboratoryPatient patient) { Patient = patient; }
    public LaboratoryPatient Patient { get; }
    public string Name => Patient.FullName;
    public string Details => $"{Patient.Identification} · {Patient.Age} años · {Patient.Sex}";
    public string Test => string.IsNullOrWhiteSpace(Patient.TestResult) ? "TEST: sin resultado" : "TEST: " + Patient.TestResult;
    private bool _selected = true;
    public bool IsSelected { get => _selected; set => SetProperty(ref _selected, value); }
}

public sealed class LaboratoryViewModel : BaseViewModel
{
    private readonly ILaboratoryImportService _import;
    private readonly ILaboratoryGenerationService _generate;
    private readonly ILaboratoryTemplateStore _templates;
    private readonly IFileSelectionService _files;
    private readonly IAppDialogService _dialogs;
    private readonly IBaseDirectoryService _directory;
    private readonly List<LaboratoryPatientRow> _all = [];
    private IReadOnlyList<string> _issues = [];
    private string? _file, _worksheet;
    private string _fileName = "Ningún Excel seleccionado.", _status = "Carga un Excel para comenzar.", _search = "";
    private DateTime _date = DateTime.Today;
    private bool _automatic, _busy, _populating, _generating;
    private CancellationTokenSource? _cancel;

    public LaboratoryViewModel(ILaboratoryImportService import, ILaboratoryGenerationService generate,
        ILaboratoryTemplateStore templates, IFileSelectionService files, IAppDialogService dialogs,
        IBaseDirectoryService directory, IFolderLauncherService launcher)
    {
        _import = import; _generate = generate; _templates = templates; _files = files; _dialogs = dialogs; _directory = directory;
        PickExcelCommand = new AsyncRelayCommand(PickExcelAsync);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        SelectAllCommand = new RelayCommand(() => SetSelection(true));
        SelectNoneCommand = new RelayCommand(() => SetSelection(false));
        ViewIssuesCommand = new AsyncRelayCommand(() => _dialogs.AlertAsync("Revisar importación", string.Join(Environment.NewLine, _issues)));
        CancelCommand = new RelayCommand(() => _cancel?.Cancel());
        Results = new(launcher, Reset);
    }
    public ObservableCollection<string> Worksheets { get; } = [];
    public ObservableCollection<LaboratoryPatientRow> Patients { get; } = [];
    public ResultActionsViewModel Results { get; }
    public ICommand PickExcelCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }
    public ICommand ViewIssuesCommand { get; }
    public ICommand CancelCommand { get; }
    public string FileName { get => _fileName; private set => SetProperty(ref _fileName, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) { OnPropertyChanged(nameof(IsIdle)); OnPropertyChanged(nameof(CanGenerate)); } } }
    public bool IsIdle => !IsBusy;
    public bool IsGenerating { get => _generating; private set => SetProperty(ref _generating, value); }
    public bool HasIssues => _issues.Count > 0;
    public bool CanGenerate => !IsBusy && _all.Any(p => p.IsSelected);
    public string SelectedCount => $"Seleccionados: {_all.Count(p => p.IsSelected)} de {_all.Count}. Visibles: {Patients.Count}.";
    public string? SelectedWorksheet
    {
        get => _worksheet;
        set { if (SetProperty(ref _worksheet, value) && !_populating && value is not null) _ = LoadSheetAsync(); }
    }
    public string Search { get => _search; set { if (SetProperty(ref _search, value)) Filter(); } }
    public DateTime ReportDate
    {
        get => _date;
        set { if (SetProperty(ref _date, value)) { OnPropertyChanged(nameof(OutputLocation)); OnPropertyChanged(nameof(ReportDateLabel)); } }
    }
    public string ReportDateLabel => "Fecha de informes: " + ReportDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    public bool AutomaticToxicology { get => _automatic; set => SetProperty(ref _automatic, value); }
    public string OutputLocation => Path.Combine(_directory.BaseDirectoryPath ?? "Selecciona una carpeta base en Ajustes", "PRUEBAS", ReportDate.ToString("ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture));
    public void Refresh() => OnPropertyChanged(nameof(OutputLocation));

    private async Task PickExcelAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        var load = false;
        try
        {
            var file = await _files.PickSingleFileAsync("Selecciona la matriz de pacientes", [".xlsx"]);
            if (file is null) return;
            var sheets = await _import.GetWorksheetNamesAsync(file.FullPath);
            Reset(); _file = file.FullPath; FileName = file.FileName;
            _populating = true;
            foreach (var sheet in sheets) Worksheets.Add(sheet);
            SelectedWorksheet = sheets.FirstOrDefault();
            load = sheets.Count > 0;
            if (!load) Status = "No hay hojas compatibles. Coloca NOMBRES Y APELLIDOS, CEDULA, EDAD y GENERO en una misma fila entre las primeras 30 filas. Se aceptan acentos y columnas en cualquier orden.";
        }
        catch (Exception e) { Status = UserProblem.Describe(e, "leer las hojas del Excel"); }
        finally { _populating = false; IsBusy = false; }
        if (load) await LoadSheetAsync();
    }
    private async Task LoadSheetAsync()
    {
        if (IsBusy || _file is null || SelectedWorksheet is null) return;
        IsBusy = true; Results.Clear(); ClearRows();
        try
        {
            var result = await _import.ImportAsync(_file, SelectedWorksheet);
            _issues = result.Issues;
            foreach (var patient in result.Patients)
            {
                var row = new LaboratoryPatientRow(patient); row.PropertyChanged += OnSelection; _all.Add(row);
            }
            Filter();
            Status = $"Hoja lista: {_all.Count} pacientes. {_issues.Count} avisos de importación.";
            OnPropertyChanged(nameof(HasIssues));
            if (HasIssues) await _dialogs.AlertAsync("Revisar la matriz", $"Se cargaron {_all.Count} pacientes. Los registros con datos incompletos se excluyeron.\n\n" + string.Join(Environment.NewLine, _issues.Take(8)) + (_issues.Count > 8 ? "\nConsulta Ver avisos para el detalle completo." : ""));
        }
        catch (Exception e) { Status = UserProblem.Describe(e, "importar los pacientes"); }
        finally { IsBusy = false; }
    }
    private void OnSelection(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LaboratoryPatientRow.IsSelected)) { OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(CanGenerate)); }
    }
    private void SetSelection(bool selected) { foreach (var row in _all) row.IsSelected = selected; }
    private void Filter()
    {
        Patients.Clear();
        foreach (var row in _all.Where(r => r.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase) || r.Patient.Identification.Contains(Search, StringComparison.Ordinal))) Patients.Add(row);
        OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(CanGenerate));
    }
    private void ClearRows()
    {
        foreach (var row in _all) row.PropertyChanged -= OnSelection;
        _all.Clear(); Patients.Clear(); _issues = [];
        OnPropertyChanged(nameof(SelectedCount)); OnPropertyChanged(nameof(HasIssues));
    }
    private void Reset()
    {
        ClearRows(); _file = null; _populating = true; Worksheets.Clear(); SelectedWorksheet = null; _populating = false;
        FileName = "Ningún Excel seleccionado."; Status = "Carga otro Excel para preparar un lote nuevo."; Results?.Clear();
    }
    private async Task GenerateAsync()
    {
        if (!CanGenerate) return;
        IsBusy = true;
        try
        {
            var templates = _templates.Load();
            var patients = _all.Where(r => r.IsSelected).Select(r => r.Patient).ToList();
            LaboratoryGenerationRequest Request(bool overwrite) => new()
            {
                Templates = templates, Patients = patients, BaseDirectory = _directory.BaseDirectoryPath ?? "",
                ReportDate = ReportDate, AutomaticToxicology = AutomaticToxicology, OverwriteConfirmed = overwrite
            };
            var request = Request(false);
            var warnings = _generate.ReviewWarnings(request);
            if (warnings.Count > 0) await _dialogs.AlertAsync("Resultados que requieren revisión manual",
                $"Se generarán igualmente los dos documentos por paciente. Los TEST vacíos, inválidos o desconocidos conservarán los resultados por defecto de la plantilla.\n\n" + string.Join(Environment.NewLine, warnings.Take(6)) + "\nEl listado completo quedará en el registro del lote.");
            var existing = _generate.ExistingFiles(request);
            if (existing.Count > 0)
            {
                if (!await _dialogs.ConfirmAsync("Reemplazar documentos existentes", $"Ya existen {existing.Count} documentos para esta fecha y estos pacientes. ¿Deseas reemplazarlos?\n\n" + string.Join(Environment.NewLine, existing.Take(4)), "Reemplazar", "Cancelar")) { Status = "No se generó el lote. Los documentos anteriores se conservaron."; return; }
                request = Request(true);
            }
            Results.Clear(); _cancel?.Dispose(); _cancel = new(); IsGenerating = true;
            var result = await _generate.GenerateAsync(request, new Progress<string>(message => Status = message), _cancel.Token);
            var succeeded = result.Files.Count(f => f.Success); var failed = result.Files.Count - succeeded;
            Results.Set(result.Files.FirstOrDefault(f => f.Success)?.ToxicologyPath, result.OutputRoot, result.LogPath);
            Status = $"Lote finalizado: {succeeded} pacientes con sus dos PDF; {failed} con errores. {result.Warnings.Count} avisos para revisión manual.";
            if (failed > 0) Status += "\nPrimer error: " + result.Files.First(f => !f.Success).Error + ". Consulta Ver errores.";
            if (succeeded > 0) RecentWorkStore.Add($"Laboratorio: {succeeded} pacientes", result.OutputRoot);
        }
        catch (OperationCanceledException) { Status = "Generación detenida. Los documentos ya generados están en la carpeta de la fecha seleccionada."; }
        catch (Exception e) { Status = UserProblem.Describe(e, "generar los resultados de laboratorio"); }
        finally { IsGenerating = false; IsBusy = false; }
    }
}
