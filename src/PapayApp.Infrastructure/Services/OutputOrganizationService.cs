using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class OutputOrganizationService : IOutputOrganizationService
{
    private readonly ISafeFileNameService _safeFileNameService;

    public OutputOrganizationService(ISafeFileNameService safeFileNameService)
    {
        _safeFileNameService = safeFileNameService;
    }

    public string EnsureOutputDirectory(
        string outputBaseDirectory,
        OccupationalEvaluationType evaluationType,
        string? centroCosto,
        string fullName)
    {
        var directoryPath = GetOutputDirectoryPath(outputBaseDirectory, evaluationType, centroCosto, fullName);
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    public string GetOutputDirectoryPath(
        string outputBaseDirectory,
        OccupationalEvaluationType evaluationType,
        string? centroCosto,
        string fullName)
    {
        var typeFolder = MedicalGenerationOptions.GetBaseDirectoryFolderName(evaluationType);
        var centroCostoFolder = string.IsNullOrWhiteSpace(centroCosto)
            ? MedicalGenerationOptions.MissingCostCenterFolderName
            : centroCosto.Trim();

        var fullNameFolder = string.IsNullOrWhiteSpace(fullName) ? "SIN NOMBRE" : fullName.Trim();
        return Path.Combine(
            outputBaseDirectory,
            _safeFileNameService.SanitizePathSegment(typeFolder, "GENERADOS"),
            _safeFileNameService.SanitizePathSegment(centroCostoFolder, MedicalGenerationOptions.MissingCostCenterFolderName),
            _safeFileNameService.SanitizePathSegment(fullNameFolder, "SIN NOMBRE"));
    }

    public string BuildOutputFilePath(
        string outputDirectory,
        OccupationalEvaluationType evaluationType,
        string fullName)
    {
        var filePrefix = MedicalGenerationOptions.GetOutputFilePrefix(evaluationType);
        var baseName = $"{filePrefix} - {fullName}".Trim();
        return _safeFileNameService.EnsureUniqueFilePath(outputDirectory, baseName, ".xlsx");
    }
}
