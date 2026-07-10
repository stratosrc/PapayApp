namespace PapayApp.Core.Models;

public sealed class GenerationError
{
    public int? RowNumber { get; init; }

    public string? Identification { get; init; }

    public required string Message { get; init; }

    public string? TechnicalDetails { get; init; }
}
