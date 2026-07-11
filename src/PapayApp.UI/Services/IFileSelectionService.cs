namespace PapayApp.UI.Services;

public interface IFileSelectionService
{
    Task<FileResult?> PickSingleFileAsync(string pickerTitle = "Selecciona un archivo");
}
