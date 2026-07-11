namespace PapayApp.UI.Services;

public interface IAppDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept = "Aceptar", string cancel = "Cancelar");
}
