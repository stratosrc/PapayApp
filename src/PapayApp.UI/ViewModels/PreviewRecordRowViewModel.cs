using System.Windows.Input;
using PapayApp.Core.Models;

namespace PapayApp.UI.ViewModels;

public sealed class PreviewRecordRowViewModel : BaseViewModel
{
    private bool _isSelected;
    private EmployeeGender _selectedGender = EmployeeGender.Masculino;

    public PreviewRecordRowViewModel(ImportedEmployeeRecord importedRecord)
    {
        ImportedRecord = importedRecord;
        _isSelected = importedRecord.Status != ImportRecordStatus.Omitted;
        SelectMaleCommand = new RelayCommand(() => SelectedGender = EmployeeGender.Masculino);
        SelectFemaleCommand = new RelayCommand(() => SelectedGender = EmployeeGender.Femenino);
        ToggleSelectionCommand = new RelayCommand(ToggleSelection, () => CanGenerate);
    }

    public ImportedEmployeeRecord ImportedRecord { get; }

    public ICommand SelectMaleCommand { get; }

    public ICommand SelectFemaleCommand { get; }

    public ICommand ToggleSelectionCommand { get; }

    public int RowNumber => ImportedRecord.RowNumber;

    public bool CanGenerate => ImportedRecord.Status != ImportRecordStatus.Omitted;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public EmployeeGender SelectedGender
    {
        get => _selectedGender;
        set
        {
            if (!SetProperty(ref _selectedGender, value))
            {
                return;
            }

            OnPropertyChanged(nameof(MaleButtonBackground));
            OnPropertyChanged(nameof(MaleButtonTextColor));
            OnPropertyChanged(nameof(FemaleButtonBackground));
            OnPropertyChanged(nameof(FemaleButtonTextColor));
            OnPropertyChanged(nameof(GenderText));
        }
    }

    public string StatusText => ImportedRecord.Status switch
    {
        ImportRecordStatus.Valid => "Valido",
        ImportRecordStatus.Incomplete => "Incompleto",
        ImportRecordStatus.Omitted => "Omitido",
        _ => ImportedRecord.Status.ToString()
    };

    public string FullNameDisplay
    {
        get
        {
            var names = $"{ImportedRecord.Record.Nombres} {ImportedRecord.Record.Apellidos}".Trim();
            return string.IsNullOrWhiteSpace(names) ? $"Fila {RowNumber}" : names;
        }
    }

    public string RowSummary => $"Fila {RowNumber} · ID: {Identification} · Centro: {CentroCosto}";

    public string Identification => ImportedRecord.Record.Identificacion ?? string.Empty;

    public string CentroCosto => ImportedRecord.Record.CentroCosto ?? string.Empty;

    public bool HasWarnings => ImportedRecord.Warnings.Count > 0;

    public string WarningsText => string.Join(Environment.NewLine, ImportedRecord.Warnings);

    public string GenderText => SelectedGender == EmployeeGender.Femenino ? "femenino" : "masculino";

    public Color MaleButtonBackground => SelectedGender == EmployeeGender.Masculino
        ? Color.FromArgb("#DDEEFF")
        : Color.FromArgb("#F7FBFD");

    public Color MaleButtonTextColor => Color.FromArgb("#1565C0");

    public Color FemaleButtonBackground => SelectedGender == EmployeeGender.Femenino
        ? Color.FromArgb("#FFE0EC")
        : Color.FromArgb("#FFF7FA");

    public Color FemaleButtonTextColor => Color.FromArgb("#C2185B");

    public EmployeeGenerationSelection ToSelection()
    {
        return new EmployeeGenerationSelection
        {
            RowNumber = ImportedRecord.RowNumber,
            Record = ImportedRecord.Record,
            NameParts = ImportedRecord.NameParts,
            Gender = SelectedGender
        };
    }

    private void ToggleSelection()
    {
        if (!CanGenerate)
        {
            return;
        }

        IsSelected = !IsSelected;
    }
}
