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
        builder.Services.AddSingleton<IAgeCalculationService, AgeCalculationService>();
        builder.Services.AddSingleton<INameSplitService, NameSplitService>();
        builder.Services.AddSingleton<ISafeFileNameService, SafeFileNameService>();
        builder.Services.AddSingleton<IOutputOrganizationService, OutputOrganizationService>();
        builder.Services.AddSingleton<HeaderNormalizer>();
        builder.Services.AddSingleton<MedicalTemplateWriter>();
        builder.Services.AddSingleton<IExcelImportService, ExcelImportService>();
        builder.Services.AddSingleton<ICertificateImportService, CertificateImportService>();
        builder.Services.AddSingleton<ICertificateGenerationService, CertificateGenerationService>();
        builder.Services.AddSingleton<IMedicalEvaluationGenerationService, MedicalEvaluationGenerationService>();
        builder.Services.AddSingleton<ICompanyStore>(_ =>
            new JsonCompanyStore(Path.Combine(FileSystem.AppDataDirectory, "companies.json")));
        builder.Services.AddSingleton<IWindowBackdropService, WindowBackdropService>();
        builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();
        builder.Services.AddSingleton<IFolderLauncherService, FolderLauncherService>();
        builder.Services.AddSingleton<IAppDialogService, AppDialogService>();
        builder.Services.AddSingleton<IBaseDirectoryService, BaseDirectoryService>();
        builder.Services.AddSingleton<IFileSelectionService, FileSelectionService>();
        builder.Services.AddSingleton<ICertificateFolioStore, CertificateFolioStore>();
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<CertificateViewModel>();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<HomePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
