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
}
