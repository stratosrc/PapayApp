namespace PapayApp.Core.Models;

public sealed class EmployeeGenerationSelection
{
    public int RowNumber { get; init; }

    public required EmployeeRecord Record { get; init; }

    public required EmployeeNameParts NameParts { get; init; }

    public EmployeeGender Gender { get; init; } = EmployeeGender.Masculino;
}
