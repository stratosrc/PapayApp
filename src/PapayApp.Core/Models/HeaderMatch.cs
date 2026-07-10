namespace PapayApp.Core.Models;

public sealed class HeaderMatch
{
    public required string OriginalHeader { get; init; }

    public required string NormalizedHeader { get; init; }

    public EmployeeField? Field { get; init; }

    public bool IsDuplicate { get; init; }

    public bool IsRecognized => Field.HasValue;

    public string CanonicalName => Field?.ToString() ?? string.Empty;
}
