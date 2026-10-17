using PapayApp.Core.Models;

namespace PapayApp.Core.Configuration;

public static class HeaderAliasConfiguration
{
    public static IReadOnlyDictionary<EmployeeField, IReadOnlyList<string>> Aliases { get; } =
        new Dictionary<EmployeeField, IReadOnlyList<string>>
        {
            [EmployeeField.Identificacion] = new[]
            {
                "Identificacion",
                "Identificación",
                "Identificaci�n",
                "Cedula",
                "Cédula",
                "Cedula de Ciudadania",
                "Cédula de Ciudadanía",
                "Numero de Cedula",
                "Número de Cédula",
                "No Cedula",
                "Nro Cedula",
                "Nro de Cedula",
                "Documento",
                "Documento de Identidad",
                "Numero de Documento",
                "Número de Documento",
                "Identificacion Empleado"
            },
            [EmployeeField.Movil] = new[]
            {
                "Movil",
                "Móvil",
                "Telefono",
                "Teléfono",
                "Celular",
                "Telefono Celular",
                "Teléfono Celular",
                "Telefono Movil",
                "Teléfono Móvil",
                "Movil 1",
                "Celular 1"
            },
            [EmployeeField.CentroCosto] = new[]
            {
                "Centro de Costo",
                "Centro Costo",
                "CentroCosto",
                "Punto de Trabajo",
                "Punto Trabajo",
                "Area",
                "Área",
                "Sede",
                "Sucursal",
                "Ubicacion",
                "Ubicación",
                "Local"
            },
            [EmployeeField.FechaNacimiento] = new[]
            {
                "Fecha de Nacimiento",
                "Fecha Nacimiento",
                "Nacimiento",
                "F Nacimiento",
                "Fecha Nac"
            },
            [EmployeeField.FechaIngreso] = new[]
            {
                "Fecha de Ingreso",
                "Fecha Ingreso",
                "Ingreso",
                "F Ingreso",
                "Fecha de Ingreso Laboral",
                "Fecha Ingreso Laboral"
            },
            [EmployeeField.FechaLiquidacion] = new[]
            {
                "Fecha de Liquidacion",
                "Fecha de Liquidación",
                "Fecha Liquidacion",
                "Fecha de Retiro",
                "Fecha Retiro",
                "F Retiro",
                "F Liquidacion",
                "Fecha Baja",
                "Baja",
                "Fecha de Baja",
                "Fecha de Salida",
                "Salida",
                "Ultimo Dia Laboral",
                "Último Día Laboral"
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
