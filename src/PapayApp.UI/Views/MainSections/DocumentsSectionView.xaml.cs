namespace PapayApp.UI.Views.MainSections;

public partial class DocumentsSectionView : ContentView
{
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
                border.Stroke = Color.FromArgb("#DCECF4");
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
