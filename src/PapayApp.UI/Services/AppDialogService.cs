namespace PapayApp.UI.Services;

public sealed class AppDialogService : IAppDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message, string accept = "Aceptar", string cancel = "Cancelar")
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return false;
        }

        return await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayAlertAsync(title, message, accept, cancel));
    }

    public async Task<string?> PromptAsync(
        string title,
        string message,
        string accept = "Aceptar",
        string cancel = "Cancelar",
        string? placeholder = null)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            return null;
        }

        return await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayPromptAsync(title, message, accept, cancel, placeholder));
    }
}
