using PapayApp.Core.Models;

namespace PapayApp.UI.ViewModels;

public sealed class PreviewRecordRowViewModel : BaseViewModel
{
    private bool _isSelected;
    private string? _primerApellido;
    private string? _segundoApellido;
    private string? _primerNombre;
    private string? _segundoNombre;

    public PreviewRecordRowViewModel(ImportedEmployeeRecord importedRecord)
    {
        ImportedRecord = importedRecord;
        _isSelected = importedRecord.Status != ImportRecordStatus.Omitted;
        _primerApellido = importedRecord.NameParts.PrimerApellido;
        _segundoApellido = importedRecord.NameParts.SegundoApellido;
        _primerNombre = importedRecord.NameParts.PrimerNombre;
        _segundoNombre = importedRecord.NameParts.SegundoNombre;
    }

    public ImportedEmployeeRecord ImportedRecord { get; }

    public int RowNumber => ImportedRecord.RowNumber;

    public bool CanGenerate => ImportedRecord.Status != ImportRecordStatus.Omitted;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string StatusText => ImportedRecord.Status switch
    {
        ImportRecordStatus.Valid => "Valido",
        ImportRecordStatus.Incomplete => "Incompleto",
        ImportRecordStatus.Omitted => "Omitido",
        _ => ImportedRecord.Status.ToString()
    };

    public string Identification => ImportedRecord.Record.Identificacion ?? string.Empty;

    public string Apellidos => ImportedRecord.Record.Apellidos ?? string.Empty;

    public string Nombres => ImportedRecord.Record.Nombres ?? string.Empty;

    public string Movil => ImportedRecord.Record.Movil ?? string.Empty;

    public string CentroCosto => ImportedRecord.Record.CentroCosto ?? string.Empty;

    public string FechaNacimiento => ImportedRecord.Record.FechaNacimiento?.ToString("yyyy/MM/dd") ?? string.Empty;

    public string FechaIngreso => ImportedRecord.Record.FechaIngreso?.ToString("yyyy/MM/dd") ?? string.Empty;

    public string FechaLiquidacion => ImportedRecord.Record.FechaLiquidacion?.ToString("yyyy/MM/dd") ?? string.Empty;

    public string Edad => ImportedRecord.Record.Edad?.ToString() ?? string.Empty;

    public string PrimerApellido
    {
        get => _primerApellido ?? string.Empty;
        set
        {
            if (SetProperty(ref _primerApellido, value))
            {
                OnPropertyChanged(nameof(FullNameDisplay));
            }
        }
    }

    public string SegundoApellido
    {
        get => _segundoApellido ?? string.Empty;
        set
        {
            if (SetProperty(ref _segundoApellido, value))
            {
                OnPropertyChanged(nameof(FullNameDisplay));
            }
        }
    }

    public string PrimerNombre
    {
        get => _primerNombre ?? string.Empty;
        set
        {
            if (SetProperty(ref _primerNombre, value))
            {
                OnPropertyChanged(nameof(FullNameDisplay));
            }
        }
    }

    public string SegundoNombre
    {
        get => _segundoNombre ?? string.Empty;
        set
        {
            if (SetProperty(ref _segundoNombre, value))
            {
                OnPropertyChanged(nameof(FullNameDisplay));
            }
        }
    }

    public string FullNameDisplay => ToSelection().NameParts.FullName;

    public bool HasWarnings => ImportedRecord.Warnings.Count > 0;

    public string WarningsText => string.Join(Environment.NewLine, ImportedRecord.Warnings);

    public string RowSummary => $"Fila {RowNumber} · ID: {Identification} · Centro: {CentroCosto}";

    public EmployeeGenerationSelection ToSelection()
    {
        return new EmployeeGenerationSelection
        {
            RowNumber = ImportedRecord.RowNumber,
            Record = ImportedRecord.Record,
            NameParts = new EmployeeNameParts
            {
                PrimerApellido = string.IsNullOrWhiteSpace(PrimerApellido) ? null : PrimerApellido.Trim(),
                SegundoApellido = string.IsNullOrWhiteSpace(SegundoApellido) ? null : SegundoApellido.Trim(),
                PrimerNombre = string.IsNullOrWhiteSpace(PrimerNombre) ? null : PrimerNombre.Trim(),
                SegundoNombre = string.IsNullOrWhiteSpace(SegundoNombre) ? null : SegundoNombre.Trim()
            }
        };
    }
}
