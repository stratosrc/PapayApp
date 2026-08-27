namespace PapayApp.UI.Views.Shell;

public partial class SideNavigationView : ContentView
{
    public SideNavigationView()
    {
        InitializeComponent();
        AttachHover(HomeNavBorder);
        AttachHover(DocumentsNavBorder);
        AttachHover(CertificatesNavBorder);
        AttachHover(FoldersNavBorder);
        AttachHover(SettingsNavBorder);
    }

    private void AttachHover(Border border)
    {
        border.HandlerChanged += (_, _) => ConfigureHover(border, 1.08, 0.96);
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
                await Task.WhenAll(
                    border.ScaleToAsync(hoverScale, 140, Easing.CubicOut),
                    border.FadeToAsync(hoverOpacity, 140, Easing.CubicOut));
            });
        }

        void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.WhenAll(
                    border.ScaleToAsync(1, 140, Easing.CubicOut),
                    border.FadeToAsync(1, 140, Easing.CubicOut));
            });
        }
#endif
    }
}
