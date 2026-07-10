using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface IMedicalEvaluationGenerationService
{
    Task<GenerationSummary> GenerateAsync(
        MedicalGenerationRequest request,
        CancellationToken cancellationToken);
}
