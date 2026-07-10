namespace PapayApp.Core.Models;

public sealed class GenerationSummary
{
    public required string OutputRootPath { get; init; }

    public bool WasCancelled { get; init; }

    public IReadOnlyList<GeneratedFileResult> Results { get; init; } = Array.Empty<GeneratedFileResult>();

    public IReadOnlyList<GenerationWarning> Warnings { get; init; } = Array.Empty<GenerationWarning>();

    public IReadOnlyList<GenerationError> Errors { get; init; } = Array.Empty<GenerationError>();

    public int SucceededCount => Results.Count(static result => result.Success);

    public int FailedCount => Results.Count(static result => !result.Success);
}
