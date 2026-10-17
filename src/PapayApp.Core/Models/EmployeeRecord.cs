namespace PapayApp.Core.Models;

public sealed class EmployeeRecord
{
    public string? Identificacion { get; set; }

    public string? Apellidos { get; set; }

    public string? Nombres { get; set; }

    public DateTime? FechaNacimiento { get; set; }

    public int? Edad { get; set; }

    public DateTime? FechaIngreso { get; set; }

    public DateTime? FechaLiquidacion { get; set; }

    public string? Movil { get; set; }

    public string? CentroCosto { get; set; }
    public string? Oficio { get; set; }
}
