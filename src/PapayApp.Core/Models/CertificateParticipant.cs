namespace PapayApp.Core.Models;

public sealed record CertificateParticipant(
    int SourceRowNumber,
    string FullName,
    string Identification);
