namespace PapayApp.UI.Views.MainSections;

public partial class DocumentsSectionView : ContentView
{
    public DocumentsSectionView()
    {
        InitializeComponent();
        UploadDropArea.HandlerChanged += OnUploadDropAreaHandlerChanged;
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
