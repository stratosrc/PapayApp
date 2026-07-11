using System.Diagnostics;

namespace PapayApp.UI.Services;

public sealed class FolderLauncherService : IFolderLauncherService
{
    public Task OpenFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("La carpeta solicitada no existe.");
        }

#if WINDOWS
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = folderPath,
            UseShellExecute = true
        });
#else
        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
#endif

        return Task.CompletedTask;
    }

    public Task OpenPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FileNotFoundException("La ruta solicitada no existe.");
        }

        if (Directory.Exists(path))
        {
            return OpenFolderAsync(path);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("La ruta solicitada no existe.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
