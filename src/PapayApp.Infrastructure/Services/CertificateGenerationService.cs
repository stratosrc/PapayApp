using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PapayApp.Infrastructure.Services;

public sealed class CertificateGenerationService : ICertificateGenerationService
{
    private const string IdentificationToken = "{cedula}";
    private static readonly XPdfFontOptions UnicodeFontOptions = new(
        PdfFontEncoding.Unicode,
        PdfFontEmbedding.EmbedCompleteFontFile);
    private static readonly Regex IdentificationTokenRegex = new(
        Regex.Escape(IdentificationToken),
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex VariableRegex = new(
        @"\{[^{}]+\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISafeFileNameService _safeFileNameService;

    public CertificateGenerationService(ISafeFileNameService safeFileNameService)
    {
        _safeFileNameService = safeFileNameService;
    }

    public Task<CertificateGenerationResult> GenerateAsync(
        CertificateGenerationRequest request,
        IProgress<CertificateGenerationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => Generate(request, progress, cancellationToken),
            cancellationToken);
    }

    private CertificateGenerationResult Generate(
        CertificateGenerationRequest request,
        IProgress<CertificateGenerationProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        Directory.CreateDirectory(request.OutputDirectory);

        CertificateFontResolver.EnsureConfigured(
            request.AlexBrushFontPath,
            request.CinzelRegularFontPath,
            request.CinzelBoldFontPath);

        var lastAssignedFolio = checked(request.LastUsedFolio + request.Participants.Count);
        var results = request.OutputMode == CertificateOutputMode.SeparateFiles
            ? GenerateSeparateFiles(request, progress, cancellationToken)
            : GenerateCombinedPdf(request, progress, cancellationToken);

        var combinedPath = results
            .Where(static result => result.Success)
            .Select(static result => result.OutputPath)
            .FirstOrDefault();

        var logPath = WriteFailureLog(request.OutputDirectory, results);

        return new CertificateGenerationResult
        {
            PreviousLastUsedFolio = request.LastUsedFolio,
            LastUsedFolio = lastAssignedFolio,
            Results = results,
            CombinedPdfPath = request.OutputMode == CertificateOutputMode.CombinedPdf
                ? combinedPath
                : null,
            LogPath = logPath
        };
    }

    private IReadOnlyList<CertificateFileResult> GenerateSeparateFiles(
        CertificateGenerationRequest request,
        IProgress<CertificateGenerationProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<CertificateFileResult>(request.Participants.Count);

        for (var index = 0; index < request.Participants.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var participant = NormalizeParticipantName(request.Participants[index]);
            var folio = checked(request.LastUsedFolio + index + 1);

            try
            {
                var pdfBytes = RenderCertificate(request, participant, folio);
                var baseName = $"{folio}_{participant.FullName}";
                var outputPath = _safeFileNameService.EnsureUniqueFilePath(
                    request.OutputDirectory,
                    baseName,
                    ".pdf");
                File.WriteAllBytes(outputPath, pdfBytes);

                results.Add(new CertificateFileResult
                {
                    Participant = participant,
                    Folio = folio,
                    Success = true,
                    OutputPath = outputPath
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(Failed(participant, folio, exception.Message));
            }

            progress?.Report(new CertificateGenerationProgressInfo(
                index + 1,
                request.Participants.Count,
                folio,
                participant.FullName));
        }

        return results;
    }

    private IReadOnlyList<CertificateFileResult> GenerateCombinedPdf(
        CertificateGenerationRequest request,
        IProgress<CertificateGenerationProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var rendered = new List<(CertificateParticipant Participant, int Folio, byte[] PdfBytes)>();
        var results = new List<CertificateFileResult>(request.Participants.Count);

        for (var index = 0; index < request.Participants.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var participant = NormalizeParticipantName(request.Participants[index]);
            var folio = checked(request.LastUsedFolio + index + 1);

            try
            {
                rendered.Add((participant, folio, RenderCertificate(request, participant, folio)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                results.Add(Failed(participant, folio, exception.Message));
            }

            progress?.Report(new CertificateGenerationProgressInfo(
                index + 1,
                request.Participants.Count,
                folio,
                participant.FullName));
        }

        if (rendered.Count == 0)
        {
            return results.OrderBy(static result => result.Folio).ToList();
        }

        var firstFolio = checked(request.LastUsedFolio + 1);
        var lastFolio = checked(request.LastUsedFolio + request.Participants.Count);
        var outputPath = _safeFileNameService.EnsureUniqueFilePath(
            request.OutputDirectory,
            $"Certificados_{firstFolio}-{lastFolio}",
            ".pdf");

        try
        {
            using var combinedDocument = new PdfDocument();
            var importedDocuments = new List<PdfDocument>(rendered.Count);

            foreach (var item in rendered)
            {
                var stream = new MemoryStream(item.PdfBytes, writable: false);
                var importedDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                importedDocuments.Add(importedDocument);
                combinedDocument.AddPage(importedDocument.Pages[0]);
            }

            combinedDocument.Save(outputPath);

            foreach (var item in rendered)
            {
                results.Add(new CertificateFileResult
                {
                    Participant = item.Participant,
                    Folio = item.Folio,
                    Success = true,
                    OutputPath = outputPath
                });
            }

            foreach (var document in importedDocuments)
            {
                document.Dispose();
            }
        }
        catch (Exception exception)
        {
            foreach (var item in rendered)
            {
                results.Add(Failed(item.Participant, item.Folio, exception.Message));
            }
        }

        return results.OrderBy(static result => result.Folio).ToList();
    }

    private static byte[] RenderCertificate(
        CertificateGenerationRequest request,
        CertificateParticipant participant,
        int folio)
    {
        using var templateDocument = PdfReader.Open(request.TemplatePath, PdfDocumentOpenMode.Import);
        if (templateDocument.PageCount == 0)
        {
            throw new InvalidDataException("La plantilla PDF no contiene páginas.");
        }

        using var outputDocument = new PdfDocument();
        var page = outputDocument.AddPage(templateDocument.Pages[0]);

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            DrawCertificateContent(graphics, request, participant, folio);
        }

        using var output = new MemoryStream();
        outputDocument.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void DrawCertificateContent(
        XGraphics graphics,
        CertificateGenerationRequest request,
        CertificateParticipant participant,
        int folio)
    {
        var goldBrush = new XSolidBrush(XColor.FromArgb(176, 134, 79));
        var blackBrush = XBrushes.Black;

        var folioFont = CreateFont("Cinzel", 10.2, XFontStyleEx.Regular);
        graphics.DrawString(
            $"{folio}-{request.CertificateDate:ddMMyyyy}",
            folioFont,
            blackBrush,
            new XRect(441, 51.7, 160, 18),
            XStringFormats.TopLeft);

        DrawSingleLineFitted(
            graphics,
            request.CertificateType.Trim().ToUpperInvariant(),
            "Cinzel",
            XFontStyleEx.Bold,
            blackBrush,
            new XRect(94, 96, 654, 57),
            46,
            18);

        DrawSingleLineFitted(
            graphics,
            participant.FullName,
            "Alex Brush",
            XFontStyleEx.Regular,
            goldBrush,
            new XRect(88, 195, 666, 58),
            46.8,
            22.8);

        var bodyText = IdentificationTokenRegex.Replace(
            request.BodyTemplate.Trim(),
            participant.Identification);
        DrawWrappedTextFitted(
            graphics,
            bodyText,
            blackBrush,
            new XRect(78, 258, 686, 100),
            16,
            7.5);

        DrawSignature(graphics, request.SignaturePngPath, new XRect(526, 384, 218, 98));

        DrawSingleLineFitted(
            graphics,
            request.SignerName.Trim(),
            "Cinzel",
            XFontStyleEx.Bold,
            goldBrush,
            new XRect(516, 488.3, 238, 21),
            14.5,
            7.5);

        DrawSingleLineFitted(
            graphics,
            request.SignerTitle.Trim(),
            "Cinzel",
            XFontStyleEx.Regular,
            blackBrush,
            new XRect(516, 512, 238, 22),
            10,
            7);
    }

    private static void DrawSignature(XGraphics graphics, string signaturePath, XRect bounds)
    {
        using var image = XImage.FromFile(signaturePath);
        var scale = Math.Min(bounds.Width / image.PointWidth, bounds.Height / image.PointHeight);
        var width = image.PointWidth * scale;
        var height = image.PointHeight * scale;
        var x = bounds.X + ((bounds.Width - width) / 2);
        var y = bounds.Y + bounds.Height - height;
        graphics.DrawImage(image, x, y, width, height);
    }

    private static void DrawSingleLineFitted(
        XGraphics graphics,
        string text,
        string family,
        XFontStyleEx style,
        XBrush brush,
        XRect bounds,
        double maximumSize,
        double minimumSize)
    {
        for (var size = maximumSize; size >= minimumSize; size -= 0.5)
        {
            var font = CreateFont(family, size, style);
            if (graphics.MeasureString(text, font).Width <= bounds.Width)
            {
                graphics.DrawString(text, font, brush, bounds, XStringFormats.TopCenter);
                return;
            }
        }

        throw new InvalidOperationException($"El texto '{text}' es demasiado largo para el espacio disponible.");
    }

    private static void DrawWrappedTextFitted(
        XGraphics graphics,
        string text,
        XBrush brush,
        XRect bounds,
        double maximumSize,
        double minimumSize)
    {
        for (var size = maximumSize; size >= minimumSize; size -= 0.5)
        {
            var font = CreateFont("Cinzel", size, XFontStyleEx.Regular);
            var lines = WrapText(graphics, text, font, bounds.Width);
            var lineHeight = size * 1.35;
            var totalHeight = lines.Count * lineHeight;
            if (totalHeight > bounds.Height)
            {
                continue;
            }

            var y = bounds.Y;
            foreach (var line in lines)
            {
                graphics.DrawString(
                    line,
                    font,
                    brush,
                    new XRect(bounds.X, y, bounds.Width, lineHeight),
                    XStringFormats.TopCenter);
                y += lineHeight;
            }

            return;
        }

        throw new InvalidOperationException(
            "El texto común es demasiado extenso para el margen disponible del certificado.");
    }

    private static IReadOnlyList<string> WrapText(
        XGraphics graphics,
        string text,
        XFont font,
        double maximumWidth)
    {
        var result = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(string.Empty);
                continue;
            }

            var currentLine = string.Empty;
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var fragments = SplitLongWord(graphics, word, font, maximumWidth);
                foreach (var fragment in fragments)
                {
                    var candidate = string.IsNullOrEmpty(currentLine)
                        ? fragment
                        : $"{currentLine} {fragment}";

                    if (graphics.MeasureString(candidate, font).Width <= maximumWidth)
                    {
                        currentLine = candidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        result.Add(currentLine);
                    }

                    currentLine = fragment;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                result.Add(currentLine);
            }
        }

        return result;
    }

    private static XFont CreateFont(string family, double size, XFontStyleEx style)
    {
        return new XFont(family, size, style, UnicodeFontOptions);
    }

    private static CertificateParticipant NormalizeParticipantName(CertificateParticipant participant)
    {
        return participant with
        {
            FullName = PersonNameFormatter.ToTitleCase(participant.FullName)
        };
    }

    private static IReadOnlyList<string> SplitLongWord(
        XGraphics graphics,
        string word,
        XFont font,
        double maximumWidth)
    {
        if (graphics.MeasureString(word, font).Width <= maximumWidth)
        {
            return new[] { word };
        }

        var fragments = new List<string>();
        var builder = new StringBuilder();

        foreach (var character in word)
        {
            var candidate = builder.ToString() + character;
            if (builder.Length > 0 && graphics.MeasureString(candidate, font).Width > maximumWidth)
            {
                fragments.Add(builder.ToString());
                builder.Clear();
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
        {
            fragments.Add(builder.ToString());
        }

        return fragments;
    }

    private string? WriteFailureLog(
        string outputDirectory,
        IReadOnlyCollection<CertificateFileResult> results)
    {
        var failed = results.Where(static result => !result.Success).ToList();
        if (failed.Count == 0)
        {
            return null;
        }

        var logPath = _safeFileNameService.EnsureUniqueFilePath(
            outputDirectory,
            $"certificados_fallidos_{DateTime.Now:yyyyMMdd_HHmmss}",
            ".txt");

        var lines = new List<string>
        {
            "LOG DE CERTIFICADOS FALLIDOS",
            $"Fecha: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
            $"Cantidad: {failed.Count}",
            string.Empty
        };

        lines.AddRange(failed.Select(result =>
            $"Folio {result.Folio} | Fila {result.Participant.SourceRowNumber} | " +
            $"{result.Participant.FullName} | {result.Participant.Identification} | {result.ErrorMessage}"));

        File.WriteAllLines(logPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return logPath;
    }

    private static void ValidateRequest(CertificateGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequireFile(request.TemplatePath, "No se encontró la plantilla PDF.");
        RequireFile(request.AlexBrushFontPath, "No se encontró la fuente Alex Brush.");
        RequireFile(request.CinzelRegularFontPath, "No se encontró la fuente Cinzel Regular.");
        RequireFile(request.CinzelBoldFontPath, "No se encontró la fuente Cinzel Bold.");
        RequireFile(request.SignaturePngPath, "No se encontró la firma PNG.");

        if (!string.Equals(Path.GetExtension(request.SignaturePngPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La firma debe ser un archivo PNG.", nameof(request));
        }

        if (request.Participants.Count == 0)
        {
            throw new ArgumentException("Debes seleccionar al menos una persona.", nameof(request));
        }

        foreach (var participant in request.Participants)
        {
            if (string.IsNullOrWhiteSpace(participant.FullName))
            {
                throw new ArgumentException("Todos los participantes deben tener un nombre.", nameof(request));
            }

            if (participant.Identification.Length != 10 ||
                participant.Identification.Any(static character => !char.IsDigit(character)))
            {
                throw new ArgumentException(
                    $"La cédula de {participant.FullName} debe contener exactamente 10 dígitos.",
                    nameof(request));
            }
        }

        if (request.LastUsedFolio < 0)
        {
            throw new ArgumentException("El último folio utilizado no puede ser negativo.", nameof(request));
        }

        _ = checked(request.LastUsedFolio + request.Participants.Count + 1);

        RequireText(request.OutputDirectory, "Debes seleccionar una carpeta de salida.");
        RequireText(request.CertificateType, "Debes escribir el tipo de certificado.");
        RequireText(request.BodyTemplate, "Debes escribir el texto del certificado.");
        RequireText(request.SignerName, "Debes escribir el nombre del firmante.");
        RequireText(request.SignerTitle, "Debes escribir el título o cargo del firmante.");

        var unsupportedVariables = VariableRegex
            .Matches(request.BodyTemplate)
            .Select(static match => match.Value)
            .Where(variable => !variable.Equals(IdentificationToken, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unsupportedVariables.Count > 0)
        {
            throw new ArgumentException(
                $"Variables no reconocidas: {string.Join(", ", unsupportedVariables)}. Solo se admite {IdentificationToken}.",
                nameof(request));
        }
    }

    private static void RequireFile(string path, string message)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException(message, path);
        }
    }

    private static void RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }
    }

    private static CertificateFileResult Failed(
        CertificateParticipant participant,
        int folio,
        string errorMessage)
    {
        return new CertificateFileResult
        {
            Participant = participant,
            Folio = folio,
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
