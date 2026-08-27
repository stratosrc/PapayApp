namespace PapayApp.Tests.Helpers;

internal static class TestPaths
{
    public static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "PapayApp.slnx")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("No se pudo localizar la raíz del repositorio.");
        }
    }

    public static string TemplatePath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Templates", "FICHA MEDICA.xlsx");

    public static string CertificateTemplatePath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Templates", "CERTIFICADO.pdf");

    public static string AlexBrushFontPath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Fonts", "AlexBrush-Regular.ttf");

    public static string CinzelRegularFontPath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Fonts", "Cinzel-Regular.ttf");

    public static string CinzelBoldFontPath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Fonts", "Cinzel-Bold.ttf");

    public static string DiplomaIconPath =>
        Path.Combine(RepositoryRoot, "src", "PapayApp.UI", "Resources", "Images", "Navigation", "diploma.png");
}
