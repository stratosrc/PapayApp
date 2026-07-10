namespace PapayApp.UI.Services;

public interface IFolderLauncherService
{
    Task OpenFolderAsync(string folderPath);
}
