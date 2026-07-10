namespace PapayApp.Core.Models;

public sealed class GeneratedFileResult
{
    public int RowNumber { get; init; }

    public string? Identification { get; init; }

    public string? FullName { get; init; }

    public string? OutputPath { get; init; }

    public bool Success { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public string? ErrorMessage { get; init; }
}
