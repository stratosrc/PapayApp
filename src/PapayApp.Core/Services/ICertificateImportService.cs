using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface ICertificateImportService
{
    Task<CertificateImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
