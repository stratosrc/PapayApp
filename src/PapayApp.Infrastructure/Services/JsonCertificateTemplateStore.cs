using System.Text.Json;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PdfSharp.Pdf.IO;

namespace PapayApp.Infrastructure.Services;

public sealed class JsonCertificateTemplateStore(string directory, string defaultTemplatePath) : ICertificateTemplateStore
{
    private readonly object _sync = new();
    public string DefaultTemplatePath => defaultTemplatePath;
    private string LibraryPath => Path.Combine(directory, "certificate-templates.json");
    private string LegacyPath => Path.Combine(directory, "certificate-template.json");

    // Return independent snapshots: editing a preset never changes saved state.
    public CertificateTemplateSettings Load()
    {
        lock (_sync)
        {
            var library = ReadLibrary();
            return library.Templates.FirstOrDefault(t => t.Id == library.ActiveTemplateId)
                ?? throw new InvalidDataException("La plantilla activa no se encuentra en la biblioteca.");
        }
    }

    public IReadOnlyList<CertificateTemplateSettings> List()
    {
        lock (_sync) return ReadLibrary().Templates;
    }

    private TemplateLibrary ReadLibrary()
    {
        if (File.Exists(LibraryPath))
            return JsonSerializer.Deserialize<TemplateLibrary>(File.ReadAllText(LibraryPath))
                ?? throw new InvalidDataException("No se pudo leer la biblioteca de plantillas.");
        var original = new CertificateTemplateSettings { Id = "original", Name = "Diseño original",
            OriginalFileName = Path.GetFileName(DefaultTemplatePath), TemplatePath = DefaultTemplatePath };
        var library = new TemplateLibrary { ActiveTemplateId = original.Id, Templates = [original] };
        if (File.Exists(LegacyPath))
        {
            var legacy = JsonSerializer.Deserialize<CertificateTemplateSettings>(File.ReadAllText(LegacyPath))
                ?? throw new InvalidDataException("No se pudo leer la plantilla anterior.");
            legacy.Id = "legacy";
            legacy.Name = "Plantilla anterior";
            legacy.OriginalFileName = Path.GetFileName(legacy.TemplatePath);
            library.Templates.Add(legacy);
            library.ActiveTemplateId = legacy.Id;
        }
        // Migrate in memory only. The first explicit Save writes the library.
        return library;
    }

    public void Save(CertificateTemplateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Name))
            throw new ArgumentException("Escribe un nombre para la plantilla.");
        using (var pdf = PdfReader.Open(settings.TemplatePath, PdfDocumentOpenMode.Import))
        {
            if (pdf.PageCount == 0) throw new InvalidDataException("El PDF no contiene páginas.");
            settings.Layout.Validate(pdf.PageCount);
        }
        lock (_sync)
        {
            var library = ReadLibrary();
            var id = string.IsNullOrWhiteSpace(settings.Id) ? Guid.NewGuid().ToString("N") : settings.Id;
            if (library.Templates.Any(t => t.Id != id && t.Name.Equals(settings.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Ya existe una plantilla con ese nombre. Elige otro nombre.");
            Directory.CreateDirectory(directory);
            // Immutable PDFs keep ongoing certificate generation independent of later saves.
            var managedPath = Path.Combine(directory, $"template-{Guid.NewGuid():N}.pdf");
            File.Copy(settings.TemplatePath, managedPath);
            var temporaryPath = LibraryPath + ".tmp";
            try
            {
                var saved = new CertificateTemplateSettings
                {
                    Id = id, Name = settings.Name.Trim(), OriginalFileName = string.IsNullOrEmpty(settings.OriginalFileName)
                        ? Path.GetFileName(settings.TemplatePath) : settings.OriginalFileName,
                    TemplatePath = managedPath, Layout = settings.Layout
                };
                var index = library.Templates.FindIndex(t => t.Id == id);
                if (index < 0) library.Templates.Add(saved);
                else library.Templates[index] = saved;
                library.ActiveTemplateId = id;
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(library, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporaryPath, LibraryPath, overwrite: true);
                settings.Id = id;
            }
            catch
            {
                File.Delete(managedPath);
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }
    }

    private sealed class TemplateLibrary
    {
        public string ActiveTemplateId { get; set; } = string.Empty;
        public List<CertificateTemplateSettings> Templates { get; set; } = [];
    }
}
