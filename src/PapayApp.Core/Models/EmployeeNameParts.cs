namespace PapayApp.Core.Models;

public sealed class EmployeeNameParts
{
    public string? PrimerApellido { get; set; }

    public string? SegundoApellido { get; set; }

    public string? PrimerNombre { get; set; }

    public string? SegundoNombre { get; set; }

    public string FullName => string.Join(
        " ",
        new[]
        {
            PrimerNombre,
            SegundoNombre,
            PrimerApellido,
            SegundoApellido
        }.Where(static value => !string.IsNullOrWhiteSpace(value))).Trim();
}
