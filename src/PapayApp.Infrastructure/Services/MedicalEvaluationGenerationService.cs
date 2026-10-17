using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class MedicalEvaluationGenerationService : IMedicalEvaluationGenerationService
{
    private readonly IOutputOrganizationService _outputOrganizationService;
    private readonly MedicalTemplateWriter _templateWriter;

    public MedicalEvaluationGenerationService(
        IOutputOrganizationService outputOrganizationService,
        MedicalTemplateWriter templateWriter)
    {
        _outputOrganizationService = outputOrganizationService;
        _templateWriter = templateWriter;
    }

    public Task<GenerationSummary> GenerateAsync(
        MedicalGenerationRequest request,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            ValidateRequest(request);

            var results = new List<GeneratedFileResult>();
            var warnings = new List<GenerationWarning>();
            var errors = new List<GenerationError>();

            for (var index = 0; index < request.Records.Count; index++)
            {
                var selection = request.Records[index];
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var centroCosto = selection.Record.CentroCosto;
                    if (string.IsNullOrWhiteSpace(selection.Record.Identificacion))
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Message = "Registro omitido por falta de identificacion."
                        });

                        results.Add(new GeneratedFileResult
                        {
                            RowNumber = selection.RowNumber,
                            Success = false,
                            ErrorMessage = "Registro omitido por falta de identificacion."
                        });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(centroCosto))
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Identification = selection.Record.Identificacion,
                            Message = "Centro de costo vacio: se usara SIN CENTRO DE COSTO."
                        });
                    }

                    if (request.EvaluationType == OccupationalEvaluationType.Reintegro)
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Identification = selection.Record.Identificacion,
                            Message = "El campo de fecha de reintegro se deja vacio porque no existe una columna especifica configurada."
                        });
                    }

                    var fullName = string.IsNullOrWhiteSpace(selection.NameParts.FullName)
                        ? $"{selection.Record.Nombres} {selection.Record.Apellidos}".Trim()
                        : selection.NameParts.FullName;

                    request.Progress?.Report(new GenerationProgressInfo
                    {
                        Current = index + 1,
                        Total = request.Records.Count,
                        FullName = string.IsNullOrWhiteSpace(fullName)
                            ? $"Fila {selection.RowNumber}"
                            : fullName
                    });

                    var outputDirectory = _outputOrganizationService.EnsureOutputDirectory(
                        request.OutputBaseDirectory,
                        request.EvaluationType,
                        centroCosto,
                        fullName);

                    var outputPath = _outputOrganizationService.BuildOutputFilePath(
                        outputDirectory,
                        request.EvaluationType,
                        fullName);

                    _templateWriter.Write(
                        request.TemplatePath,
                        outputPath,
                        selection,
                        request.EvaluationType,
                        request.GeneratedAt,
                        request.AttentionDate,
                        request.Company,
                        request.LogoPath);

                    results.Add(new GeneratedFileResult
                    {
                        RowNumber = selection.RowNumber,
                        Identification = selection.Record.Identificacion,
                        FullName = fullName,
                        OutputPath = outputPath,
                        Success = true,
                        Warnings = string.IsNullOrWhiteSpace(centroCosto)
                            ? new[] { "Se uso la carpeta SIN CENTRO DE COSTO." }
                            : Array.Empty<string>()
                    });
                }
                catch (Exception exception)
                {
                    errors.Add(new GenerationError
                    {
                        RowNumber = selection.RowNumber,
                        Identification = selection.Record.Identificacion,
                        Message = "Ocurrio un error al generar el archivo individual.",
                        TechnicalDetails = exception.Message
                    });

                    results.Add(new GeneratedFileResult
                    {
                        RowNumber = selection.RowNumber,
                        Identification = selection.Record.Identificacion,
                        FullName = selection.NameParts.FullName,
                        Success = false,
                        ErrorMessage = exception.Message
                    });
                }
            }

            var outputRootPath = Path.Combine(
                request.OutputBaseDirectory,
                MedicalGenerationOptions.GetBaseDirectoryFolderName(request.EvaluationType));

            var logPath = WriteGenerationLog(outputRootPath, request, results, warnings, errors);

            return new GenerationSummary
            {
                OutputRootPath = outputRootPath,
                LogPath = logPath,
                Results = results,
                Warnings = warnings,
                Errors = errors
            };
        }, cancellationToken);
    }

    private static void ValidateRequest(MedicalGenerationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.TemplatePath) || !File.Exists(request.TemplatePath))
        {
            throw new FileNotFoundException("No se encontro la plantilla correspondiente al tipo de evaluacion.", request.TemplatePath);
        }

        if (string.IsNullOrWhiteSpace(request.OutputBaseDirectory) || !Directory.Exists(request.OutputBaseDirectory))
        {
            throw new DirectoryNotFoundException("No se encontro el directorio base configurado.");
        }

        if (request.Company is null ||
            string.IsNullOrWhiteSpace(request.Company.Institution) ||
            string.IsNullOrWhiteSpace(request.Company.Ruc) ||
            string.IsNullOrWhiteSpace(request.Company.Ciiu))
        {
            throw new InvalidDataException("Selecciona una empresa valida antes de generar.");
        }

        if (!string.IsNullOrWhiteSpace(request.LogoPath) && !LogoImageValidator.IsValid(request.LogoPath))
        {
            throw new InvalidDataException("El logotipo seleccionado no es un archivo PNG valido.");
        }
    }

    private static string WriteGenerationLog(
        string outputRootPath,
        MedicalGenerationRequest request,
        IReadOnlyList<GeneratedFileResult> results,
        IReadOnlyList<GenerationWarning> warnings,
        IReadOnlyList<GenerationError> errors)
    {
        Directory.CreateDirectory(outputRootPath);
        var logsDirectory = Path.Combine(outputRootPath, "Logs");
        Directory.CreateDirectory(logsDirectory);

        var logPath = Path.Combine(logsDirectory, $"generacion-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new List<string>
        {
            "PapayApp - Log de generacion",
            $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Tipo evaluacion: {request.EvaluationType}",
            $"Plantilla: {request.TemplatePath}",
            $"Empresa: {request.Company.DisplayName}",
            $"Fecha de atencion: {(request.AttentionDate.HasValue ? request.AttentionDate.Value.ToString("dd/MM/yyyy") : "En blanco")}",
            $"Logotipo: {request.LogoPath ?? "No seleccionado"}",
            $"Destino base: {request.OutputBaseDirectory}",
            $"Registros solicitados: {request.Records.Count}",
            $"Generados: {results.Count(result => result.Success)}",
            $"Fallidos u omitidos: {results.Count(result => !result.Success)}",
            string.Empty,
            "Resultados:"
        };

        foreach (var result in results.OrderBy(static item => item.RowNumber))
        {
            lines.Add(
                $"Fila {result.RowNumber} | {(result.Success ? "OK" : "ERROR")} | {result.Identification ?? "-"} | {result.FullName ?? "-"} | {result.OutputPath ?? result.ErrorMessage ?? "-"}");
        }

        if (warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Advertencias:");
            foreach (var warning in warnings.OrderBy(static item => item.RowNumber))
            {
                lines.Add($"Fila {warning.RowNumber} | {warning.Identification ?? "-"} | {warning.Message}");
            }
        }

        if (errors.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Errores:");
            foreach (var error in errors.OrderBy(static item => item.RowNumber))
            {
                lines.Add($"Fila {error.RowNumber} | {error.Identification ?? "-"} | {error.Message} | {error.TechnicalDetails ?? "-"}");
            }
        }

        File.WriteAllLines(logPath, lines);
        return logPath;
    }
}
