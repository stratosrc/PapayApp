using PapayApp.Core.Configuration;
using PapayApp.Core.Models;

namespace PapayApp.Tests.Helpers;

internal static class TestPaths
{
    private const string TinyJpegBase64 = "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==";

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

    public static string MedicalTemplatePath(OccupationalEvaluationType evaluationType) =>
        Path.Combine(
            RepositoryRoot,
            "src",
            "PapayApp.UI",
            "Resources",
            "Raw",
            "Templates",
            MedicalTemplateOptions.GetTemplateFileName(evaluationType));

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

    public static byte[] TinyJpegBytes => Convert.FromBase64String(TinyJpegBase64);
}
