namespace PapayApp.Core.Services;

public interface ISafeFileNameService
{
    string SanitizePathSegment(string? value, string fallback);

    string EnsureUniqueFilePath(string directoryPath, string fileNameWithoutExtension, string extension);
}
