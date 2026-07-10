using PapayApp.UI.ViewModels;
using PapayApp.UI.Services;

namespace PapayApp.UI.Pages;

public partial class WelcomePage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowBackdropService _windowBackdropService;

    public WelcomePage(
        WelcomeViewModel viewModel,
        IServiceProvider serviceProvider,
        IWindowBackdropService windowBackdropService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _serviceProvider = serviceProvider;
        _windowBackdropService = windowBackdropService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _windowBackdropService.TryApplySoftBackdrop();
        await Task.Delay(3800);

        var homePage = _serviceProvider.GetRequiredService<HomePage>();
        await Navigation.PushAsync(homePage, false);
        Navigation.RemovePage(this);
    }
}
