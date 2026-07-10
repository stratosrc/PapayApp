using Microsoft.Extensions.Logging;
using PapayApp.Core.Services;
using PapayApp.Infrastructure.Services;
using PapayApp.UI.Pages;
using PapayApp.UI.Services;
using PapayApp.UI.ViewModels;

namespace PapayApp.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<IDateService, SystemDateService>();
        builder.Services.AddSingleton<IWindowBackdropService, WindowBackdropService>();
        builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();
        builder.Services.AddSingleton<IBaseDirectoryService, BaseDirectoryService>();
        builder.Services.AddSingleton<IFileSelectionService, FileSelectionService>();
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<HomePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
