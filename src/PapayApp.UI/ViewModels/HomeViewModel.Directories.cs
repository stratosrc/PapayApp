namespace PapayApp.UI.ViewModels;

public sealed partial class HomeViewModel
{
    private async Task RefreshDirectoriesAsync()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            DirectoryEntries.Clear();
            FoldersCurrentPathText = "Ruta actual: -";
            FoldersStatusMessage = "No hay un directorio base configurado.";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
            return;
        }

        var targetDirectory = _foldersCurrentDirectoryPath;
        if (string.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            targetDirectory = baseDirectory;
        }

        await LoadDirectoryEntriesAsync(targetDirectory);
    }

    private async Task NavigateUpDirectoryAsync()
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(_foldersCurrentDirectoryPath))
        {
            return;
        }

        var currentDirectory = NormalizeDirectoryPath(_foldersCurrentDirectoryPath);
        var rootDirectory = NormalizeDirectoryPath(baseDirectory);
        if (string.Equals(currentDirectory, rootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return;
        }

        await LoadDirectoryEntriesAsync(parentDirectory);
    }

    private async Task OpenCurrentDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_foldersCurrentDirectoryPath))
        {
            return;
        }

        try
        {
            await _folderLauncherService.OpenFolderAsync(_foldersCurrentDirectoryPath);
        }
        catch (Exception exception)
        {
            FoldersStatusMessage = $"No fue posible abrir la carpeta actual: {exception.Message}";
        }
    }

    private async Task ChangeBaseDirectoryAsync()
    {
        try
        {
            var selectedPath = await _baseDirectoryService.ChangeBaseDirectoryAsync();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            _foldersCurrentDirectoryPath = selectedPath;
            RefreshSettingsState();
            SettingsStatusMessage = "Ruta base actualizada correctamente.";
            await RefreshDirectoriesAsync();
        }
        catch (Exception exception)
        {
            SettingsStatusMessage = $"No fue posible cambiar la ruta base: {exception.Message}";
        }
    }

    private async Task LoadDirectoryEntriesAsync(string directoryPath)
    {
        var baseDirectory = _baseDirectoryService.BaseDirectoryPath;
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return;
        }

        var normalizedBaseDirectory = NormalizeDirectoryPath(baseDirectory);
        var normalizedTargetDirectory = NormalizeDirectoryPath(directoryPath);
        if (!IsWithinBaseDirectory(normalizedBaseDirectory, normalizedTargetDirectory))
        {
            normalizedTargetDirectory = normalizedBaseDirectory;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(normalizedTargetDirectory);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException("La carpeta solicitada no existe.");
            }

            var directoryEntries = directoryInfo
                .EnumerateDirectories()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(directory => CreateDirectoryEntry(directory, normalizedBaseDirectory))
                .Concat(
                    directoryInfo
                        .EnumerateFiles()
                        .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(file => CreateFileEntry(file, normalizedBaseDirectory)))
                .ToList();

            DirectoryEntries.Clear();
            foreach (var entry in directoryEntries)
            {
                DirectoryEntries.Add(entry);
            }

            _foldersCurrentDirectoryPath = normalizedTargetDirectory;
            FoldersCurrentPathText = $"Ruta actual: {BuildRelativeDirectoryPath(normalizedBaseDirectory, normalizedTargetDirectory)}";
            FoldersStatusMessage = directoryEntries.Count == 0
                ? "Esta carpeta aun no contiene archivos ni subcarpetas."
                : $"{directoryEntries.Count} elementos en la carpeta actual.";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
        }
        catch (Exception exception)
        {
            DirectoryEntries.Clear();
            FoldersStatusMessage = $"No fue posible leer la carpeta: {exception.Message}";
            OnPropertyChanged(nameof(HasDirectoryEntries));
            OnPropertyChanged(nameof(CanNavigateUpDirectory));
        }

        await Task.CompletedTask;
    }

    private DirectoryEntryViewModel CreateDirectoryEntry(DirectoryInfo directoryInfo, string baseDirectory)
    {
        return new DirectoryEntryViewModel
        {
            Name = directoryInfo.Name,
            RelativePath = BuildRelativeEntryPath(baseDirectory, directoryInfo.FullName),
            ItemTypeText = "Carpeta",
            ModifiedText = $"Actualizada: {directoryInfo.LastWriteTime:dd/MM/yyyy HH:mm}",
            IconSource = "folder_directory.png",
            IsDirectory = true,
            OpenCommand = new AsyncRelayCommand(() => LoadDirectoryEntriesAsync(directoryInfo.FullName))
        };
    }

    private DirectoryEntryViewModel CreateFileEntry(FileInfo fileInfo, string baseDirectory)
    {
        return new DirectoryEntryViewModel
        {
            Name = fileInfo.Name,
            RelativePath = BuildRelativeEntryPath(baseDirectory, fileInfo.FullName),
            ItemTypeText = "Archivo",
            ModifiedText = $"Actualizado: {fileInfo.LastWriteTime:dd/MM/yyyy HH:mm}",
            IconSource = "excel_file.png",
            IsDirectory = false,
            OpenCommand = new AsyncRelayCommand(() => OpenFileAsync(fileInfo.FullName))
        };
    }

    private async Task OpenFileAsync(string filePath)
    {
        try
        {
            await _folderLauncherService.OpenPathAsync(filePath);
        }
        catch (Exception exception)
        {
            FoldersStatusMessage = $"No fue posible abrir el archivo: {exception.Message}";
        }
    }

    private static string BuildRelativeDirectoryPath(string baseDirectory, string currentDirectory)
    {
        var relativePath = Path.GetRelativePath(baseDirectory, currentDirectory);
        return relativePath == "." ? "Base" : relativePath;
    }

    private static string BuildRelativeEntryPath(string baseDirectory, string entryPath)
    {
        return Path.GetRelativePath(baseDirectory, entryPath);
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsWithinBaseDirectory(string baseDirectory, string targetDirectory)
    {
        if (string.Equals(baseDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedBaseDirectory = $"{baseDirectory}{Path.DirectorySeparatorChar}";
        var normalizedTargetDirectory = $"{targetDirectory}{Path.DirectorySeparatorChar}";
        return normalizedTargetDirectory.StartsWith(normalizedBaseDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
