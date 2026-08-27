namespace PapayApp.Core.Models;

public sealed class CertificateImportResult
{
    public string WorksheetName { get; init; } = string.Empty;

    public IReadOnlyList<CertificateParticipant> Participants { get; init; } =
        Array.Empty<CertificateParticipant>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool IsValid => Errors.Count == 0 && Participants.Count > 0;
}
