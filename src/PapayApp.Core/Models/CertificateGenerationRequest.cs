namespace PapayApp.Core.Models;

public sealed class CertificateGenerationRequest
{
    public required string TemplatePath { get; init; }

    public required string AlexBrushFontPath { get; init; }

    public required string CinzelRegularFontPath { get; init; }

    public required string CinzelBoldFontPath { get; init; }

    public required string OutputDirectory { get; init; }

    public required string SignaturePngPath { get; init; }

    public required string CertificateType { get; init; }

    public required string BodyTemplate { get; init; }

    public required string SignerName { get; init; }

    public required string SignerTitle { get; init; }

    public required IReadOnlyList<CertificateParticipant> Participants { get; init; }

    public CertificateOutputMode OutputMode { get; init; }

    public DateTime CertificateDate { get; init; }

    public int LastUsedFolio { get; init; }
}
