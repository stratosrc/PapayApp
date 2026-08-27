using PdfSharp.Fonts;

namespace PapayApp.Infrastructure.Services;

internal sealed class CertificateFontResolver : IFontResolver
{
    private const string AlexBrushFace = "PapayApp-AlexBrush-Regular";
    private const string CinzelRegularFace = "PapayApp-Cinzel-Regular";
    private const string CinzelBoldFace = "PapayApp-Cinzel-Bold";

    private static readonly object SyncRoot = new();
    private static bool _configured;
    private static byte[]? _alexBrushBytes;
    private static byte[]? _cinzelRegularBytes;
    private static byte[]? _cinzelBoldBytes;

    public static void EnsureConfigured(
        string alexBrushFontPath,
        string cinzelRegularFontPath,
        string cinzelBoldFontPath)
    {
        lock (SyncRoot)
        {
            if (_configured)
            {
                return;
            }

            _alexBrushBytes = File.ReadAllBytes(alexBrushFontPath);
            _cinzelRegularBytes = File.ReadAllBytes(cinzelRegularFontPath);
            _cinzelBoldBytes = File.ReadAllBytes(cinzelBoldFontPath);

            GlobalFontSettings.FontResolver = new CertificateFontResolver();
            _configured = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (familyName.Equals("Alex Brush", StringComparison.OrdinalIgnoreCase))
        {
            return new FontResolverInfo(AlexBrushFace, false, italic);
        }

        if (familyName.Equals("Cinzel", StringComparison.OrdinalIgnoreCase))
        {
            return new FontResolverInfo(
                bold ? CinzelBoldFace : CinzelRegularFace,
                false,
                italic);
        }

        return null;
    }

    public byte[]? GetFont(string faceName)
    {
        return faceName switch
        {
            AlexBrushFace => _alexBrushBytes,
            CinzelRegularFace => _cinzelRegularBytes,
            CinzelBoldFace => _cinzelBoldBytes,
            _ => null
        };
    }
}
