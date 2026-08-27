namespace PapayApp.Core.Models;

public sealed record CertificateGenerationProgressInfo(
    int Processed,
    int Total,
    int Folio,
    string ParticipantName);
