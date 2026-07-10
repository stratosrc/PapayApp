using PapayApp.UI.Services;
using PapayApp.UI.ViewModels;

namespace PapayApp.UI.Pages;

public partial class HomePage : ContentPage
{
    private readonly IWindowBackdropService _windowBackdropService;
    private readonly IBaseDirectoryService _baseDirectoryService;
    private bool _baseDirectoryChecked;

    public HomePage(
        HomeViewModel viewModel,
        IWindowBackdropService windowBackdropService,
        IBaseDirectoryService baseDirectoryService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _windowBackdropService = windowBackdropService;
        _baseDirectoryService = baseDirectoryService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _windowBackdropService.TryApplySoftBackdrop();

        if (_baseDirectoryChecked)
        {
            return;
        }

        _baseDirectoryChecked = true;
        await _baseDirectoryService.EnsureConfiguredAsync(this);
    }
}
