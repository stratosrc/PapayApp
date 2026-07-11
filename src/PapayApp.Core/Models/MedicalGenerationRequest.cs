namespace PapayApp.Core.Models;

public sealed class MedicalGenerationRequest
{
    public required string TemplatePath { get; init; }

    public required string OutputBaseDirectory { get; init; }

    public required OccupationalEvaluationType EvaluationType { get; init; }

    public required DateTime GeneratedAt { get; init; }

    public IReadOnlyList<EmployeeGenerationSelection> Records { get; init; } = Array.Empty<EmployeeGenerationSelection>();

    public IProgress<GenerationProgressInfo>? Progress { get; init; }
}
