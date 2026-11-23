using PapayApp.UI.Services;
using PapayApp.UI.ViewModels;
using PapayApp.Core.Services;

namespace PapayApp.UI.Pages;

public partial class HomePage : ContentPage
{
    private readonly IWindowBackdropService _windowBackdropService;
    private readonly IBaseDirectoryService _baseDirectoryService;
    private readonly HomeViewModel _viewModel;
    private View? _activeSection;
    private readonly CertificateViewModel _certificateViewModel;
    private readonly LaboratoryViewModel _laboratoryViewModel;
    private bool _baseDirectoryChecked;

    public HomePage(
        HomeViewModel viewModel,
        CertificateViewModel certificateViewModel,
        LaboratoryViewModel laboratoryViewModel,
        ILaboratoryTemplateStore laboratoryTemplateStore,
        IWindowBackdropService windowBackdropService,
        IBaseDirectoryService baseDirectoryService,
        ICertificateTemplateStore certificateTemplateStore,
        IFileSelectionService fileSelectionService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _certificateViewModel = certificateViewModel;
        _laboratoryViewModel = laboratoryViewModel;
        LaboratorySection.BindingContext = laboratoryViewModel;
        SettingsSection.ConfigureLaboratoryTemplates(laboratoryTemplateStore, fileSelectionService);
        CertificatesSection.BindingContext = certificateViewModel;
        TemplatesSection.ConfigureCertificateEditor(certificateTemplateStore, fileSelectionService);
        _viewModel = viewModel;

        viewModel.SectionChanged += OnSectionChanged;
        _windowBackdropService = windowBackdropService;
        _baseDirectoryService = baseDirectoryService;
        _activeSection = HomeSection;
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

    private async void OnSectionChanged(object? sender, MainSection section)
    {
        if (section == MainSection.Certificates) _certificateViewModel.RefreshTemplate();
        if (section == MainSection.Laboratory) _laboratoryViewModel.Refresh();
        await AnimateSectionChangeAsync(section);
    }

    private async Task AnimateSectionChangeAsync(MainSection section)
    {
        var nextSection = GetSectionView(section);
        if (ReferenceEquals(_activeSection, nextSection))
        {
            return;
        }

        if (_activeSection is not null)
        {
            await Task.WhenAll(
                _activeSection.FadeToAsync(0, 220, Easing.CubicIn),
                _activeSection.TranslateToAsync(-10, 0, 220, Easing.CubicIn));
            _activeSection.IsVisible = false;
            _activeSection.TranslationX = 0;
        }

        nextSection.Opacity = 0;
        nextSection.TranslationX = 10;
        nextSection.IsVisible = true;

        await Task.WhenAll(
            nextSection.FadeToAsync(1, 340, Easing.CubicOut),
            nextSection.TranslateToAsync(0, 0, 340, Easing.CubicOut));

        _activeSection = nextSection;
    }

    private View GetSectionView(MainSection section)
    {
        return section switch
        {
            MainSection.Home => HomeSection,
            MainSection.Documents => DocumentsSection,
            MainSection.Certificates => CertificatesSection,
            MainSection.Laboratory => LaboratorySection,
            MainSection.Folders => FoldersSection,
            MainSection.Settings => SettingsSection,
            MainSection.Templates => TemplatesSection,
            _ => HomeSection
        };
    }

    private async void OnGenerationCompleted(object? sender, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            DisplayAlertAsync("Generacion finalizada", message, "Aceptar"));
    }
}
