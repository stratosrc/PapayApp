using System.Text;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class SafeFileNameService : ISafeFileNameService
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public string SanitizePathSegment(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);

        foreach (var character in source)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.');
        sanitized = string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;

        if (ReservedNames.Contains(sanitized))
        {
            sanitized = $"{sanitized}_";
        }

        if (sanitized.Length > 90)
        {
            sanitized = sanitized[..90].TrimEnd(' ', '.');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    public string EnsureUniqueFilePath(string directoryPath, string fileNameWithoutExtension, string extension)
    {
        var sanitizedBaseName = SanitizePathSegment(fileNameWithoutExtension, "Archivo");
        var candidate = Path.Combine(directoryPath, sanitizedBaseName + extension);

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 2; i < 1000; i++)
        {
            candidate = Path.Combine(directoryPath, $"{sanitizedBaseName} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No fue posible generar un nombre de archivo único.");
    }
}
