using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface IOutputOrganizationService
{
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
