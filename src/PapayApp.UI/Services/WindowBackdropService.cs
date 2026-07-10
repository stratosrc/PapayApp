namespace PapayApp.UI.Services;

public sealed class WindowBackdropService : IWindowBackdropService
{
    public bool TryApplySoftBackdrop()
    {
#if WINDOWS
        try
        {
            var mauiWindow = Application.Current?.Windows.FirstOrDefault();
            if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                return true;
            }
        }
        catch
        {
            return false;
        }
#endif

        return false;
    }
}
