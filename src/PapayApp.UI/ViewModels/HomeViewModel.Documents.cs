using System.ComponentModel;
using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;

namespace PapayApp.UI.ViewModels;

public sealed partial class HomeViewModel
{
    private async Task LoadCompaniesAsync()
    {
        try
        {
            var companies = await _companyStore.GetAllAsync();
            Companies.Clear();
            foreach (var company in companies.OrderBy(static company => company.Institution, StringComparer.CurrentCultureIgnoreCase))
            {
                Companies.Add(company);
            }

            if (SelectedCompany is null && Companies.Count > 0)
            {
                SelectedCompany = Companies[0];
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible cargar las empresas registradas: {exception.Message}";
        }
    }

    private async Task RegisterCompanyAsync()
    {
        var institution = await _appDialogService.PromptAsync(
            "Registrar empresa",
            "Institucion del sistema:",
            placeholder: "Nombre de la institucion");
        if (institution is null)
        {
            return;
        }

        var ruc = await _appDialogService.PromptAsync(
            "Registrar empresa",
            "RUC:",
            placeholder: "0992424362001");
        if (ruc is null)
        {
            return;
        }

        var ciiu = await _appDialogService.PromptAsync(
            "Registrar empresa",
            "CIIU:",
            placeholder: "N8110.00");
        if (ciiu is null)
        {
            return;
        }

        try
        {
            var company = await _companyStore.AddAsync(new CompanyProfile
            {
                Institution = institution,
                Ruc = ruc,
                Ciiu = ciiu
            });

            Companies.Add(company);
            var sorted = Companies
                .OrderBy(static item => item.Institution, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            Companies.Clear();
            foreach (var item in sorted)
            {
                Companies.Add(item);
            }

            SelectedCompany = company;
            StatusMessage = "Empresa registrada correctamente.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible registrar la empresa: {exception.Message}";
        }
    }

    private async Task DeleteSelectedCompanyAsync()
    {
        if (SelectedCompany is null)
        {
            StatusMessage = "Selecciona una empresa para eliminar.";
            return;
        }

        var company = SelectedCompany;
        var confirmed = await _appDialogService.ConfirmAsync(
            "Eliminar empresa",
            $"Se eliminara {company.DisplayName}. Deseas continuar?",
            "Eliminar",
            "Cancelar");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _companyStore.DeleteAsync(company.Id);
            Companies.Remove(company);
            SelectedCompany = Companies.FirstOrDefault();
            StatusMessage = "Empresa eliminada correctamente.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible eliminar la empresa: {exception.Message}";
        }
    }

    private async Task PickLogoAsync()
    {
        try
        {
            var file = await _fileSelectionService.PickSingleFileAsync(
                "Selecciona el logotipo PNG o JPEG",
                [".png", ".jpg", ".jpeg"]);
            if (file is null)
            {
                return;
            }

            if (!LogoImageValidator.IsValid(file.FullPath))
            {
                StatusMessage = "El archivo seleccionado no es una imagen PNG o JPEG valida.";
                return;
            }

            _logoPath = file.FullPath;
            SelectedLogoFileName = file.FileName;
            StatusMessage = "Logotipo seleccionado correctamente.";
            UpdatePreGenerationSummary();
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible seleccionar el logotipo: {exception.Message}";
        }
    }

    private void ClearLogo()
    {
        _logoPath = null;
        SelectedLogoFileName = null;
        StatusMessage = "Logotipo retirado de la generacion actual.";
        UpdatePreGenerationSummary();
    }

    public async Task LoadMasterFileAsync(string filePath, string? displayName = null)
    {
        _masterFilePath = filePath;
        SelectedUploadFileName = displayName ?? Path.GetFileName(filePath);
        AvailableWorksheets.Clear();
        OnPropertyChanged(nameof(HasMultipleWorksheets));
        _allPreviewRecords.Clear();
        PreviewRecords.Clear();
        PreviewSearchText = string.Empty;
        ImportSummaryText = "Leyendo archivo maestro...";
        GenerationSummaryText = string.Empty;
        PreGenerationSummaryText = string.Empty;

        try
        {
            IsBusy = true;
            StatusMessage = "Detectando hojas compatibles del archivo maestro...";
            var worksheets = await _excelImportService.GetWorksheetNamesAsync(filePath, CancellationToken.None);

            foreach (var worksheet in worksheets)
            {
                AvailableWorksheets.Add(worksheet);
            }

            OnPropertyChanged(nameof(HasMultipleWorksheets));
            var preferredWorksheet = ResolvePreferredWorksheetName(AvailableWorksheets);
            SelectedWorksheet = preferredWorksheet;

            if (worksheets.Count == 0)
            {
                _allPreviewRecords.Clear();
                PreviewRecords.Clear();
                ImportSummaryText = "No se encontraron hojas de ingresos o retiro compatibles.";
                StatusMessage = "El archivo maestro no contiene hojas utilizables.";
                OnPropertyChanged(nameof(CanGenerate));
            }
            else if (string.IsNullOrWhiteSpace(preferredWorksheet))
            {
                _allPreviewRecords.Clear();
                PreviewRecords.Clear();
                ImportSummaryText = BuildWorksheetMissingMessage();
                StatusMessage = BuildWorksheetMissingMessage();
                OnPropertyChanged(nameof(CanGenerate));
            }
            else
            {
                StatusMessage = "Archivo maestro cargado correctamente.";
            }
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
            var file = await _fileSelectionService.PickSingleFileAsync("Selecciona el archivo maestro");
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
            StatusMessage = $"Leyendo la hoja {SelectedWorksheet}...";

            var preview = await _excelImportService.ImportWorksheetAsync(
                _masterFilePath,
                SelectedWorksheet,
                _previewCancellationTokenSource.Token);

            StatusMessage = $"Validando registros de la hoja {preview.WorksheetName}...";

            _allPreviewRecords.Clear();
            PreviewRecords.Clear();
            foreach (var record in preview.Records)
            {
                var rowViewModel = new PreviewRecordRowViewModel(record)
                {
                    IsSelected = record.Status != ImportRecordStatus.Omitted
                };
                rowViewModel.PropertyChanged += OnPreviewRecordPropertyChanged;
                _allPreviewRecords.Add(rowViewModel);
            }

            ApplyPreviewFilter();

            ImportSummaryText =
                $"Hoja: {preview.WorksheetName} | Validos: {preview.ValidCount} | Incompletos: {preview.IncompleteCount} | Omitidos: {preview.OmittedCount}";

            StatusMessage = $"Archivo listo para generar. Se cargaron {preview.Records.Count} registros desde {preview.WorksheetName}.";
            OnPropertyChanged(nameof(CanGenerate));
            UpdatePreGenerationSummary();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "La carga fue cancelada.";
        }
        catch (Exception exception)
        {
            _allPreviewRecords.Clear();
            PreviewRecords.Clear();
            ImportSummaryText = "No fue posible leer el archivo.";
            StatusMessage = $"Error al leer la hoja seleccionada: {exception.Message}";
            OnPropertyChanged(nameof(CanGenerate));
            UpdatePreGenerationSummary();
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

        if (SelectedCompany is null)
        {
            StatusMessage = "Selecciona una empresa antes de generar.";
            return;
        }

        var templatePath = GetTemplatePath(_selectedEvaluationType.Value);
        if (!File.Exists(templatePath))
        {
            StatusMessage = $"No se encontro la plantilla {Path.GetFileName(templatePath)}.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(_logoPath) && !LogoImageValidator.IsValid(_logoPath))
        {
            StatusMessage = "El logotipo seleccionado ya no existe o no es una imagen PNG o JPEG valida.";
            return;
        }

        var selectedRows = _allPreviewRecords
            .Where(static row => row.CanGenerate && row.IsSelected)
            .ToList();

        var selectedRecords = selectedRows
            .Select(static row => row.ToSelection())
            .ToList();

        if (selectedRecords.Count == 0)
        {
            StatusMessage = "No hay registros validos para generar.";
            return;
        }

        var existingFiles = FindExistingSimilarFiles(baseDirectory, _selectedEvaluationType.Value, selectedRows);
        if (existingFiles.Count > 0)
        {
            var previewList = string.Join(Environment.NewLine, existingFiles.Take(5));
            var suffix = existingFiles.Count > 5
                ? $"{Environment.NewLine}...y {existingFiles.Count - 5} mas."
                : string.Empty;

            var confirmed = await _appDialogService.ConfirmAsync(
                "Confirmar sobrescritura",
                $"Ya existen archivos similares para esta generacion:{Environment.NewLine}{previewList}{suffix}{Environment.NewLine}{Environment.NewLine}Deseas continuar de todos modos?",
                "Continuar",
                "Cancelar");

            if (!confirmed)
            {
                StatusMessage = "Generacion cancelada para evitar sobrescrituras o duplicados.";
                return;
            }
        }

        _generationCancellationTokenSource?.Cancel();
        _generationCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsGenerating = true;
            StatusMessage = $"Preparando la generacion de {selectedRecords.Count} archivos...";
            GenerationSummaryText = string.Empty;
            var progress = new Progress<GenerationProgressInfo>(update =>
            {
                StatusMessage = $"Generando {update.Current}/{update.Total}: {update.FullName}";
            });

            var summary = await _generationService.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = templatePath,
                    OutputBaseDirectory = baseDirectory,
                    EvaluationType = _selectedEvaluationType.Value,
                    GeneratedAt = _dateService.Today,
                    AttentionDate = IncludeMedicalAttentionDate ? MedicalAttentionDate : null,
                    Company = SelectedCompany,
                    LogoPath = _logoPath,
                    Records = selectedRecords,
                    Progress = progress
                },
                _generationCancellationTokenSource.Token);

            GenerationSummaryText = BuildGenerationSummaryText(summary);
            StatusMessage = summary.FailedCount == 0
                ? "Generacion completada correctamente."
                : "Generacion completada con advertencias o errores parciales.";

            var completionMessage = summary.FailedCount == 0
                ? $"Se generaron {summary.SucceededCount} archivos correctamente."
                : $"Se generaron {summary.SucceededCount} archivos y {summary.FailedCount} quedaron con errores.";

            if (!string.IsNullOrWhiteSpace(summary.LogPath))
            {
                completionMessage = $"{completionMessage}{Environment.NewLine}Log: {summary.LogPath}";
            }

            GenerationCompleted?.Invoke(this, completionMessage);
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
        UpdatePreGenerationSummary();
        StatusMessage = $"Tipo de evaluacion seleccionado: {MedicalGenerationOptions.GetVisibleFolderName(evaluationType)}.";
    }

    private void SelectPreferredWorksheetIfAvailable()
    {
        if (AvailableWorksheets.Count == 0)
        {
            return;
        }

        var preferredWorksheet = ResolvePreferredWorksheetName(AvailableWorksheets);
        SelectedWorksheet = preferredWorksheet;

        if (string.IsNullOrWhiteSpace(preferredWorksheet))
        {
            PreviewRecords.Clear();
            ImportSummaryText = BuildWorksheetMissingMessage();
            StatusMessage = BuildWorksheetMissingMessage();
            OnPropertyChanged(nameof(CanGenerate));
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

        var preferredNames = GetPreferredWorksheetKeywords();

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

        // GetWorksheetNamesAsync solo devuelve hojas con los encabezados requeridos.
        // El nombre de la hoja puede ser generico (por ejemplo, "Reporte").
        return worksheets[0];
    }

    private string[] GetPreferredWorksheetKeywords()
    {
        return _selectedEvaluationType == OccupationalEvaluationType.Retiro
            ? RetiroWorksheetKeywords
            : IngresoWorksheetKeywords;
    }

    private string BuildWorksheetMissingMessage()
    {
        return _selectedEvaluationType == OccupationalEvaluationType.Retiro
            ? "No se encontro una hoja de retiro o salida compatible en el archivo maestro."
            : "No se encontro una hoja de ingresos compatible en el archivo maestro.";
    }

    private void ApplyPreviewFilter()
    {
        var search = _previewSearchText.Trim();
        var filteredRows = string.IsNullOrWhiteSpace(search)
            ? _allPreviewRecords
            : _allPreviewRecords
                .Where(row =>
                    row.FullNameDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    row.Identification.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        PreviewRecords.Clear();
        foreach (var row in filteredRows)
        {
            PreviewRecords.Add(row);
        }

        OnPropertyChanged(nameof(PreviewResultsCountText));
        OnPropertyChanged(nameof(CanGenerate));
    }

    private void OnPreviewRecordPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewRecordRowViewModel.IsSelected) or nameof(PreviewRecordRowViewModel.SelectedGender))
        {
            OnPropertyChanged(nameof(CanGenerate));
            UpdatePreGenerationSummary();
        }
    }

    private void UpdatePreGenerationSummary()
    {
        var selectedCount = _allPreviewRecords.Count(static row => row.CanGenerate && row.IsSelected);
        if (!_selectedEvaluationType.HasValue || string.IsNullOrWhiteSpace(SelectedWorksheet) || selectedCount == 0)
        {
            PreGenerationSummaryText = string.Empty;
            return;
        }

        var exampleNames = _allPreviewRecords
            .Where(static row => row.CanGenerate && row.IsSelected)
            .Take(3)
            .Select(static row => row.FullNameDisplay)
            .ToList();

        var destinationRoot = Path.Combine(
            _baseDirectoryService.BaseDirectoryPath ?? string.Empty,
            MedicalGenerationOptions.GetBaseDirectoryFolderName(_selectedEvaluationType.Value));

        var examplesText = exampleNames.Count == 0
            ? "-"
            : string.Join(", ", exampleNames);

        PreGenerationSummaryText = string.Join(
            Environment.NewLine,
            $"Tipo: {MedicalGenerationOptions.GetVisibleFolderName(_selectedEvaluationType.Value)}",
            $"Hoja origen: {SelectedWorksheet}",
            $"Plantilla: {MedicalTemplateOptions.GetTemplateFileName(_selectedEvaluationType.Value)}",
            $"Empresa: {SelectedCompany?.DisplayName ?? "No seleccionada"}",
            $"Fecha de atencion: {(IncludeMedicalAttentionDate ? MedicalAttentionDate.ToString("dd/MM/yyyy") : "En blanco")}",
            $"Logotipo: {SelectedLogoFileName ?? "No seleccionado"}",
            $"Personas seleccionadas: {selectedCount}",
            $"Destino base: {destinationRoot}",
            $"Vista previa: {examplesText}");
    }

    private IReadOnlyList<string> FindExistingSimilarFiles(
        string baseDirectory,
        OccupationalEvaluationType evaluationType,
        IEnumerable<PreviewRecordRowViewModel> selectedRows)
    {
        var matches = new List<string>();
        var filePrefix = MedicalGenerationOptions.GetOutputFilePrefix(evaluationType);

        foreach (var row in selectedRows)
        {
            var selection = row.ToSelection();
            var fullName = string.IsNullOrWhiteSpace(selection.NameParts.FullName)
                ? row.FullNameDisplay
                : selection.NameParts.FullName;

            var outputDirectory = _outputOrganizationService.GetOutputDirectoryPath(
                baseDirectory,
                evaluationType,
                row.CentroCosto,
                fullName);

            if (!Directory.Exists(outputDirectory))
            {
                continue;
            }

            var expectedPrefix = $"{filePrefix} - {fullName}".Trim();
            var existingFiles = Directory
                .EnumerateFiles(outputDirectory, "*.xlsx", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .ToList();

            if (existingFiles.Count > 0)
            {
                matches.Add($"{fullName}: {string.Join(", ", existingFiles)}");
            }
        }

        return matches;
    }

    private static string BuildGenerationSummaryText(GenerationSummary summary)
    {
        var lines = new List<string>
        {
            $"Generados: {summary.SucceededCount}",
            $"Fallidos: {summary.FailedCount}"
        };

        var failedItems = summary.Results
            .Where(static result => !result.Success)
            .Select(result => string.IsNullOrWhiteSpace(result.FullName)
                ? result.Identification ?? $"Fila {result.RowNumber}"
                : result.FullName)
            .ToList();

        if (failedItems.Count > 0)
        {
            lines.Add($"Con error: {string.Join(", ", failedItems)}");
        }

        if (!string.IsNullOrWhiteSpace(summary.LogPath))
        {
            lines.Add($"Log: {summary.LogPath}");
        }

        return string.Join(Environment.NewLine, lines);
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
