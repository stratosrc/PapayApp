using PapayApp.Core.Models;

namespace PapayApp.Core.Configuration;

public static class HeaderAliasConfiguration
{
    public static IReadOnlyDictionary<EmployeeField, IReadOnlyList<string>> Aliases { get; } =
        new Dictionary<EmployeeField, IReadOnlyList<string>>
        {
            [EmployeeField.Identificacion] = new[]
            {
                "Identificación",
                "Identificacion",
                "Cédula",
                "Cedula"
            },
            [EmployeeField.Movil] = new[]
            {
                "Móvil",
                "Movil",
                "Teléfono",
                "Telefono",
                "Celular"
            },
            [EmployeeField.CentroCosto] = new[]
            {
                "Centro de Costo",
                "Centro Costo",
                "CentroCosto",
                "Punto de Trabajo",
                "Punto Trabajo"
            },
            [EmployeeField.FechaNacimiento] = new[]
            {
                "Fecha de Nacimiento",
                "Fecha Nacimiento",
                "Nacimiento"
            },
            [EmployeeField.FechaIngreso] = new[]
            {
                "Fecha de Ingreso",
                "Fecha Ingreso",
                "Ingreso"
            },
            [EmployeeField.FechaLiquidacion] = new[]
            {
                "Fecha de Liquidación",
                "Fecha Liquidacion",
                "Fecha de Retiro",
                "Fecha Retiro",
                "Último Día Laboral",
                "Ultimo Dia Laboral"
            },
            [EmployeeField.Apellidos] = new[]
            {
                "Apellidos",
                "Apellido",
                "Apellidos Completos"
            },
            [EmployeeField.Nombres] = new[]
            {
                "Nombres",
                "Nombre",
                "Nombres Completos"
            },
            [EmployeeField.Edad] = new[]
            {
                "Edad"
            }
        };
}
