namespace PapayApp.UI.Services;

public interface IFolderLauncherService
{
    Task OpenFolderAsync(string folderPath);
    Task OpenPathAsync(string path);
}
