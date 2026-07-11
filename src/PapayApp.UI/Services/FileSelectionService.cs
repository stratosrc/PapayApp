namespace PapayApp.UI.Services;

public sealed class FileSelectionService : IFileSelectionService
{
    public async Task<FileResult?> PickSingleFileAsync(string pickerTitle = "Selecciona un archivo")
    {
        var options = new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = new[] { ".xlsx" },
                [DevicePlatform.macOS] = new[] { "xlsx" }
            })
        };

        return await FilePicker.Default.PickAsync(options);
    }
}
