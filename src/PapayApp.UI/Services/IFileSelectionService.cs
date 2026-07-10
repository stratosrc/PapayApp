namespace PapayApp.UI.Services;

public interface IFileSelectionService
{
    Task<FileResult?> PickSingleFileAsync();
}
