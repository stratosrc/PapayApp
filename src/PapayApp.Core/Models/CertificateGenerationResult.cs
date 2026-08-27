namespace PapayApp.Core.Models;

public sealed class CertificateGenerationResult
{
    public IReadOnlyList<CertificateFileResult> Results { get; init; } =
        Array.Empty<CertificateFileResult>();

    public int PreviousLastUsedFolio { get; init; }

    public int LastUsedFolio { get; init; }

    public int NextFolio => checked(LastUsedFolio + 1);

    public string? CombinedPdfPath { get; init; }

    public string? LogPath { get; init; }

    public int SucceededCount => Results.Count(static result => result.Success);

    public int FailedCount => Results.Count(static result => !result.Success);
}
