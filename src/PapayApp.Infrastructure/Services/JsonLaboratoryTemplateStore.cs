using System.Text.Json;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class JsonLaboratoryTemplateStore(string directory, string templatesDirectory) : ILaboratoryTemplateStore
{
    private string SettingsPath => Path.Combine(directory, "laboratory-templates.json");
    public LaboratoryTemplateSettings Defaults => new()
    {
        ToxicologyPath = Path.Combine(templatesDirectory, "LAB_TOXICOLOGICOS.docx"),
        SerologyPath = Path.Combine(templatesDirectory, "LAB_SEROLOGICOS.docx")
    };
    public LaboratoryTemplateSettings Load() => File.Exists(SettingsPath)
        ? JsonSerializer.Deserialize<LaboratoryTemplateSettings>(File.ReadAllText(SettingsPath)) ?? throw new InvalidDataException("No se pudo leer la configuración de laboratorio.") : Defaults;
    public void Save(LaboratoryTemplateSettings settings)
    {
        LaboratoryDocxTemplate.Validate(settings.ToxicologyPath);
        LaboratoryDocxTemplate.Validate(settings.SerologyPath);
        Directory.CreateDirectory(directory);
        var toxicology = Path.Combine(directory, $"toxicology-{Guid.NewGuid():N}.docx");
        var serology = Path.Combine(directory, $"serology-{Guid.NewGuid():N}.docx");
        var temp = SettingsPath + ".tmp";
        try
        {
            File.Copy(settings.ToxicologyPath, toxicology);
            File.Copy(settings.SerologyPath, serology);
            File.WriteAllText(temp, JsonSerializer.Serialize(new LaboratoryTemplateSettings
            { ToxicologyPath = toxicology, SerologyPath = serology, ToxicologyName = settings.ToxicologyName, SerologyName = settings.SerologyName }));
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch { File.Delete(toxicology); File.Delete(serology); File.Delete(temp); throw; }
    }
}
