namespace PapayApp.Infrastructure.Services;

public static class LogoImageValidator
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] JpegStartSignature = [255, 216, 255];
    private static readonly byte[] JpegEndSignature = [255, 217];

    public static bool IsValid(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[PngSignature.Length];
            var bytesRead = stream.Read(header);

            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead == PngSignature.Length && header.SequenceEqual(PngSignature);
            }

            if (bytesRead < JpegStartSignature.Length ||
                !header[..JpegStartSignature.Length].SequenceEqual(JpegStartSignature) ||
                stream.Length < JpegStartSignature.Length + JpegEndSignature.Length)
            {
                return false;
            }

            stream.Seek(-JpegEndSignature.Length, SeekOrigin.End);
            Span<byte> trailer = stackalloc byte[JpegEndSignature.Length];
            return stream.Read(trailer) == JpegEndSignature.Length && trailer.SequenceEqual(JpegEndSignature);
        }
        catch
        {
            return false;
        }
    }
}
