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

            foreach (var selection in request.Records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var centroCosto = selection.Record.CentroCosto;
                    if (string.IsNullOrWhiteSpace(selection.Record.Identificacion))
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Message = "Registro omitido por falta de identificación."
                        });

                        results.Add(new GeneratedFileResult
                        {
                            RowNumber = selection.RowNumber,
                            Success = false,
                            ErrorMessage = "Registro omitido por falta de identificación."
                        });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(centroCosto))
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Identification = selection.Record.Identificacion,
                            Message = "Centro de costo vacío: se usará SIN CENTRO DE COSTO."
                        });
                    }

                    if (request.EvaluationType == OccupationalEvaluationType.Reintegro)
                    {
                        warnings.Add(new GenerationWarning
                        {
                            RowNumber = selection.RowNumber,
                            Identification = selection.Record.Identificacion,
                            Message = "El campo de fecha de reintegro se deja vacío porque no existe una columna específica configurada."
                        });
                    }

                    var fullName = string.IsNullOrWhiteSpace(selection.NameParts.FullName)
                        ? $"{selection.Record.Nombres} {selection.Record.Apellidos}".Trim()
                        : selection.NameParts.FullName;

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
                        request.GeneratedAt);

                    results.Add(new GeneratedFileResult
                    {
                        RowNumber = selection.RowNumber,
                        Identification = selection.Record.Identificacion,
                        FullName = fullName,
                        OutputPath = outputPath,
                        Success = true,
                        Warnings = string.IsNullOrWhiteSpace(centroCosto)
                            ? new[] { "Se usó la carpeta SIN CENTRO DE COSTO." }
                            : Array.Empty<string>()
                    });
                }
                catch (Exception exception)
                {
                    errors.Add(new GenerationError
                    {
                        RowNumber = selection.RowNumber,
                        Identification = selection.Record.Identificacion,
                        Message = "Ocurrió un error al generar el archivo individual.",
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

            return new GenerationSummary
            {
                OutputRootPath = Path.Combine(
                    request.OutputBaseDirectory,
                    MedicalGenerationOptions.GetBaseDirectoryFolderName(request.EvaluationType)),
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
            throw new FileNotFoundException("No se encontró la plantilla FICHA MEDICA.xlsx.", request.TemplatePath);
        }

        if (string.IsNullOrWhiteSpace(request.OutputBaseDirectory) || !Directory.Exists(request.OutputBaseDirectory))
        {
            throw new DirectoryNotFoundException("No se encontró el directorio base configurado.");
        }
    }
}
