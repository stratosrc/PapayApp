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
}
