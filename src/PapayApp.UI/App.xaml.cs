using PapayApp.UI.Pages;

namespace PapayApp.UI;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var welcomePage = _serviceProvider.GetRequiredService<WelcomePage>();
        var navigationPage = new NavigationPage(welcomePage)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Colors.Transparent
        };

        return new Window(navigationPage)
        {
            Title = "PapayApp"
        };
    }
}
