namespace PapayApp.Core.Models;

public sealed class ImportedEmployeeRecord
{
    public int RowNumber { get; init; }

    public required EmployeeRecord Record { get; init; }

    public required EmployeeNameParts NameParts { get; init; }

    public ImportRecordStatus Status { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
