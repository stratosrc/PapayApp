using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface ICertificateGenerationService
{
    Task<CertificateGenerationResult> GenerateAsync(
        CertificateGenerationRequest request,
        IProgress<CertificateGenerationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}
