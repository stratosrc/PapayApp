namespace PapayApp.Core.Models;

public sealed record DocumentRecord(
    Guid Id,
    string FileName,
    string OriginalPath,
    string? ModifiedPath,
    string? PdfPath,
    DateTimeOffset CreatedAt);
