using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface ILaboratoryImportService
{
    Task<IReadOnlyList<string>> GetWorksheetNamesAsync(string path, CancellationToken cancellationToken = default);
    Task<LaboratoryImportResult> ImportAsync(string path, string worksheet, CancellationToken cancellationToken = default);
}

public interface ILaboratoryTemplateStore
{
    LaboratoryTemplateSettings Load();
    void Save(LaboratoryTemplateSettings settings);
    LaboratoryTemplateSettings Defaults { get; }
}

public interface ILaboratoryGenerationService
{
    IReadOnlyList<string> ExistingFiles(LaboratoryGenerationRequest request);
    IReadOnlyList<string> ReviewWarnings(LaboratoryGenerationRequest request);
    Task<LaboratoryGenerationResult> GenerateAsync(LaboratoryGenerationRequest request,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
