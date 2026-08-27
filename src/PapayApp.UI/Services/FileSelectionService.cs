namespace PapayApp.UI.Services;

public sealed class FileSelectionService : IFileSelectionService
{
    public async Task<FileResult?> PickSingleFileAsync(
        string pickerTitle = "Selecciona un archivo",
        IReadOnlyCollection<string>? allowedExtensions = null)
    {
        var extensions = allowedExtensions is null || allowedExtensions.Count == 0
            ? new[] { ".xlsx" }
            : allowedExtensions
                .Select(static extension => extension.StartsWith('.') ? extension : $".{extension}")
                .ToArray();

        var options = new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = extensions,
                [DevicePlatform.macOS] = extensions.Select(static extension => extension.TrimStart('.'))
            })
        };

        return await FilePicker.Default.PickAsync(options);
    }
}
