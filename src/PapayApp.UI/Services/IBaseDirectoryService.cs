namespace PapayApp.UI.Services;

public interface IBaseDirectoryService
{
    string? BaseDirectoryPath { get; }
    Task<bool> EnsureConfiguredAsync(Page hostPage);
    Task<string?> ChangeBaseDirectoryAsync();
}
