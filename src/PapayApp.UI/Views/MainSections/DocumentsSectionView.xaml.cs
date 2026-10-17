namespace PapayApp.UI.Views.MainSections;

public partial class DocumentsSectionView : ContentView
{
    private bool _isPreviewTransitioning;
    private bool? _isCompactLayout;

    private void OnToggleCompanyOptionsClicked(object? sender, EventArgs e)
    {
        CompanyOptions.IsVisible = !CompanyOptions.IsVisible;
        if (sender is Button button)
        {
            button.Text = CompanyOptions.IsVisible ? "Empresa y logotipo ▴" : "Empresa y logotipo ▾";
        }
    }

    private void OnWorkspaceSizeChanged(object? sender, EventArgs e)
    {
        if (GeneratorWorkspace.Width <= 0)
            return;

        var compact = GeneratorWorkspace.Width < 820;
        if (_isCompactLayout == compact)
            return;

        _isCompactLayout = compact;
        GeneratorWorkspace.ColumnDefinitions.Clear();
        GeneratorWorkspace.RowDefinitions.Clear();
        GeneratorWorkspace.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = compact ? GridLength.Star : new GridLength(340)
        });
        GeneratorWorkspace.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = compact ? new GridLength(0) : GridLength.Star
        });
        GeneratorWorkspace.RowDefinitions.Add(new RowDefinition
        {
            Height = compact ? GridLength.Auto : GridLength.Star
        });
        GeneratorWorkspace.RowDefinitions.Add(new RowDefinition
        {
            Height = compact ? GridLength.Star : new GridLength(0)
        });
        ConfigurationScroll.HeightRequest = compact ? 260 : -1;
        Grid.SetColumn(PreviewPanel, compact ? 0 : 1);
        Grid.SetRow(PreviewPanel, compact ? 1 : 0);
    }

    public DocumentsSectionView()
    {
        InitializeComponent();
        UploadDropArea.HandlerChanged += OnUploadDropAreaHandlerChanged;
        AttachHover(PeriodicaOptionBorder);
        AttachHover(PreocupacionalOptionBorder);
        AttachHover(ReintegroOptionBorder);
        AttachHover(RetiroOptionBorder);
    }

    private void AttachHover(Border border)
    {
        border.HandlerChanged += (_, _) => ConfigureHover(border, 1.02, 0.97);
    }

    private async void OnTogglePreviewExpandedClicked(object? sender, EventArgs e)
    {
        if (_isPreviewTransitioning || BindingContext is not ViewModels.HomeViewModel viewModel)
        {
            return;
        }

        _isPreviewTransitioning = true;

        try
        {
            if (viewModel.IsPreviewExpanded)
            {
                await PreviewPanel.FadeToAsync(0.92, 100, Easing.CubicOut);
                viewModel.TogglePreviewExpandedCommand.Execute(null);
                await Task.WhenAll(
                    PreviewPanel.FadeToAsync(1, 180, Easing.CubicOut),
                    FadeInAsync(DocumentsTitle),
                    FadeInAsync(DocumentControls),
                    FadeInAsync(ImportStatusPanel),
                    FadeInAsync(PreGenerationSummary));
            }
            else
            {
                await Task.WhenAll(
                    PreviewPanel.FadeToAsync(0.92, 100, Easing.CubicOut),
                    FadeOutAsync(DocumentsTitle),
                    FadeOutAsync(DocumentControls),
                    FadeOutAsync(ImportStatusPanel),
                    FadeOutAsync(PreGenerationSummary));

                viewModel.TogglePreviewExpandedCommand.Execute(null);
                await PreviewPanel.FadeToAsync(1, 180, Easing.CubicOut);
            }
        }
        finally
        {
            _isPreviewTransitioning = false;
        }
    }

    private static async Task FadeOutAsync(VisualElement element)
    {
        if (element.IsVisible)
        {
            await element.FadeToAsync(0, 140, Easing.CubicOut);
        }
    }

    private static async Task FadeInAsync(VisualElement element)
    {
        if (element.IsVisible)
        {
            element.Opacity = 0;
            await element.FadeToAsync(1, 180, Easing.CubicOut);
        }
    }

    private static void ConfigureHover(Border border, double hoverScale, double hoverOpacity)
    {
#if WINDOWS
        if (border.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement element)
        {
            return;
        }

        element.PointerEntered -= OnPointerEntered;
        element.PointerExited -= OnPointerExited;
        element.PointerCanceled -= OnPointerExited;
        element.PointerEntered += OnPointerEntered;
        element.PointerExited += OnPointerExited;
        element.PointerCanceled += OnPointerExited;

        void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                border.Stroke = Color.FromArgb("#6DAEDB");
                await Task.WhenAll(
                    border.ScaleToAsync(hoverScale, 140, Easing.CubicOut),
                    border.FadeToAsync(hoverOpacity, 140, Easing.CubicOut));
            });
        }

        void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                border.Stroke = Color.FromArgb("#405868");
                await Task.WhenAll(
                    border.ScaleToAsync(1, 140, Easing.CubicOut),
                    border.FadeToAsync(1, 140, Easing.CubicOut));
            });
        }
#endif
    }

    private void OnUploadDropAreaHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (UploadDropArea.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement element)
        {
            element.AllowDrop = true;
            element.DragOver -= OnNativeDragOver;
            element.Drop -= OnNativeDrop;
            element.DragOver += OnNativeDragOver;
            element.Drop += OnNativeDrop;
        }
#endif
    }

#if WINDOWS
    private static void OnNativeDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
    }

    private async void OnNativeDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var file = items.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
        if (file is null)
        {
            return;
        }

        if (BindingContext is ViewModels.HomeViewModel viewModel)
        {
            await viewModel.LoadMasterFileAsync(file.Path, file.Name);
        }
    }
#endif
}
