using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class NameSplitService : INameSplitService
{
    public EmployeeNameParts Split(string? nombres, string? apellidos)
    {
        var surnameParts = SplitTerms(apellidos);
        var nameParts = SplitTerms(nombres);

        return new EmployeeNameParts
        {
            PrimerApellido = surnameParts.First,
            SegundoApellido = surnameParts.Second,
            PrimerNombre = nameParts.First,
            SegundoNombre = nameParts.Second
        };
    }

    private static (string? First, string? Second) SplitTerms(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return (null, null);
        }

        if (parts.Length == 1)
        {
            return (parts[0], null);
        }

        return (parts[0], string.Join(" ", parts.Skip(1)));
    }
}
