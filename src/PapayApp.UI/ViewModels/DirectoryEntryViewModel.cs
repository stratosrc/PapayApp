using System.Windows.Input;

namespace PapayApp.UI.ViewModels;

public sealed class DirectoryEntryViewModel
{
    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public required string ItemTypeText { get; init; }

    public required string ModifiedText { get; init; }

    public required string IconSource { get; init; }

    public bool IsDirectory { get; init; }

    public required ICommand OpenCommand { get; init; }
}
