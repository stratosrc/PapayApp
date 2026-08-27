namespace PapayApp.Core.Models;

public sealed class CertificateFileResult
{
    public required CertificateParticipant Participant { get; init; }

    public int Folio { get; init; }

    public bool Success { get; init; }

    public string? OutputPath { get; init; }

    public string? ErrorMessage { get; init; }
}
