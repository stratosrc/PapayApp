namespace PapayApp.Core.Models;

public sealed class GenerationWarning
{
    public int? RowNumber { get; init; }

    public string? Identification { get; init; }

    public required string Message { get; init; }
}
