using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface IOutputOrganizationService
{
    string GetOutputDirectoryPath(
        string outputBaseDirectory,
        OccupationalEvaluationType evaluationType,
        string? centroCosto,
        string fullName);

    string EnsureOutputDirectory(
        string outputBaseDirectory,
        OccupationalEvaluationType evaluationType,
        string? centroCosto,
        string fullName);

    string BuildOutputFilePath(
        string outputDirectory,
        OccupationalEvaluationType evaluationType,
        string fullName);
}
