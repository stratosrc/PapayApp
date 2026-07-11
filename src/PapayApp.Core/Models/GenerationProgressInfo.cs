namespace PapayApp.Core.Models;

public sealed class GenerationProgressInfo
{
    public required int Current { get; init; }

    public required int Total { get; init; }

    public required string FullName { get; init; }
}
