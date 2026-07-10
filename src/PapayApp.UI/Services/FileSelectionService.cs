namespace PapayApp.UI.Services;

public sealed class FileSelectionService : IFileSelectionService
{
    public async Task<FileResult?> PickSingleFileAsync()
    {
        var options = new PickOptions
        {
            PickerTitle = "Selecciona un archivo",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = new[] { ".xlsx", ".xls" },
                [DevicePlatform.macOS] = new[] { "xlsx", "xls" }
            })
        };

        return await FilePicker.Default.PickAsync(options);
    }
}
