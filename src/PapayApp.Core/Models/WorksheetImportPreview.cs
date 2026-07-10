namespace PapayApp.Core.Models;

public sealed class WorksheetImportPreview
{
    public required string WorksheetName { get; init; }

    public int HeaderRowNumber { get; init; }

    public IReadOnlyList<HeaderMatch> Headers { get; init; } = Array.Empty<HeaderMatch>();

    public IReadOnlyList<ImportedEmployeeRecord> Records { get; init; } = Array.Empty<ImportedEmployeeRecord>();

    public IReadOnlyList<string> RecognizedHeaders { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> UnrecognizedHeaders { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DuplicateHeaders { get; init; } = Array.Empty<string>();

    public int ValidCount { get; init; }

    public int IncompleteCount { get; init; }

    public int OmittedCount { get; init; }
}
