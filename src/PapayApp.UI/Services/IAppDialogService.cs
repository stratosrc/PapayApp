namespace PapayApp.UI.Services;

public interface IAppDialogService
{
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Aceptar", string cancel = "Cancelar");

    Task<string?> PromptAsync(
        string title,
        string message,
        string accept = "Aceptar",
        string cancel = "Cancelar",
        string? placeholder = null);
}
