using System.Windows.Input;
using PapayApp.Core.Models;
using PapayApp.UI.Services;

namespace PapayApp.UI.ViewModels;
public sealed class CertificateInputViewModel : BaseViewModel
{
    private string _value = "";
    private string _status = "Selecciona una imagen PNG para esta firma.";
    public CertificateInputViewModel(CertificateElement element, IFileSelectionService files)
    {
        Key = element.InputKey; Name = element.Name; IsSignature = element.Kind == CertificateElementKind.Signature;
        PickImageCommand = new AsyncRelayCommand(async () =>
        {
            try
            {
                var file = await files.PickSingleFileAsync($"Selecciona {Name}", [".png"]);
                if (file is null) return;
                Value = file.FullPath;
                Status = "Seleccionada: " + file.FileName;
            }
            catch (Exception e) { Status = UserProblem.Describe(e, $"seleccionar {Name}"); }
        });
    }
    public string Key { get; }
    public string Name { get; }
    public bool IsSignature { get; }
    public bool IsText => !IsSignature;
    public string Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value)) return;
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImagePath));
            if (IsSignature) Status = string.IsNullOrWhiteSpace(value) ? "Selecciona una imagen PNG para esta firma." : "Seleccionada: " + Path.GetFileName(value);
        }
    }
    public bool HasImage => IsSignature && !string.IsNullOrWhiteSpace(Value);
    public string? ImagePath => IsSignature ? Value : null;
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public ICommand PickImageCommand { get; }
}
