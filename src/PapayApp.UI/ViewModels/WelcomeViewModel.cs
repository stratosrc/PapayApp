using PapayApp.Core.Services;

namespace PapayApp.UI.ViewModels;

public sealed class WelcomeViewModel : BaseViewModel
{
    public WelcomeViewModel(IDateService dateService)
    {
        DateText = dateService.GetWelcomeDateText();
    }

    public string Greeting => "Bienvenida";

    public string DateText { get; }
}
