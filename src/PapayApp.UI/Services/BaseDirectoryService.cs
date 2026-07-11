namespace PapayApp.UI.Services;

public sealed class BaseDirectoryService : IBaseDirectoryService
{
    private const string BaseDirectoryKey = "PapayApp.BaseDirectory";

    private static readonly string[] RequiredFolders =
    {
        "EVALUACION MEDICA - PERIODICA",
        "EVALUACION MEDICA - PREOCUPACIONAL",
        "EVALUACION MEDICA - REINTEGRO",
        "EVALUACION MEDICA - RETIRO"
    };

    private readonly IFolderPickerService _folderPickerService;

    public BaseDirectoryService(IFolderPickerService folderPickerService)
    {
        _folderPickerService = folderPickerService;
    }

    public string? BaseDirectoryPath => Preferences.Default.Get<string?>(BaseDirectoryKey, null);

    public async Task<bool> EnsureConfiguredAsync(Page hostPage)
    {
        var savedPath = BaseDirectoryPath;
        if (!string.IsNullOrWhiteSpace(savedPath) && Directory.Exists(savedPath))
        {
            if (HasRequiredFolders(savedPath))
            {
                return true;
            }

            return await ResolveMissingFoldersAsync(hostPage, savedPath);
        }

        await hostPage.DisplayAlertAsync(
            "Directorio base",
            "Selecciona el directorio base de PapayApp. Dentro se crearan las carpetas de evaluacion medica.",
            "Seleccionar");

        var selectedPath = await _folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            await hostPage.DisplayAlertAsync(
                "Directorio requerido",
                "PapayApp necesita un directorio base para guardar los archivos que generara.",
                "Entendido");

            return false;
        }

        CreateRequiredFolders(selectedPath);
        Preferences.Default.Set(BaseDirectoryKey, selectedPath);
        return true;
    }

    public async Task<string?> ChangeBaseDirectoryAsync()
    {
        var selectedPath = await _folderPickerService.PickFolderAsync();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        CreateRequiredFolders(selectedPath);
        Preferences.Default.Set(BaseDirectoryKey, selectedPath);
        return selectedPath;
    }

    private async Task<bool> ResolveMissingFoldersAsync(Page hostPage, string savedPath)
    {
        var action = await hostPage.DisplayActionSheetAsync(
            "No se detectaron todas las carpetas requeridas en el directorio base.",
            null,
            null,
            "Recrear carpetas",
            "Cambiar directorio base");

        if (action == "Cambiar directorio base")
        {
            var selectedPath = await _folderPickerService.PickFolderAsync();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return false;
            }

            CreateRequiredFolders(selectedPath);
            Preferences.Default.Set(BaseDirectoryKey, selectedPath);
            return true;
        }

        if (action == "Recrear carpetas")
        {
            CreateRequiredFolders(savedPath);
            return true;
        }

        return false;
    }

    private static bool HasRequiredFolders(string baseDirectoryPath)
    {
        return RequiredFolders.All(folderName => Directory.Exists(Path.Combine(baseDirectoryPath, folderName)));
    }

    private static void CreateRequiredFolders(string baseDirectoryPath)
    {
        foreach (var folderName in RequiredFolders)
        {
            Directory.CreateDirectory(Path.Combine(baseDirectoryPath, folderName));
        }
    }
}
