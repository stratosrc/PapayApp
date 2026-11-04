namespace PapayApp.Core.Models;

public sealed class CertificateLayout
{
    public List<CertificateElement> Elements { get; set; } = [];

    public bool Uses(string token) => Elements.Any(e => string.IsNullOrWhiteSpace(e.InputKey) && e.Kind == CertificateElementKind.Text &&
        e.Content.Contains(token, StringComparison.OrdinalIgnoreCase));

    public bool RequiresSignature => Elements.Any(e => e.Kind == CertificateElementKind.Signature && string.IsNullOrWhiteSpace(e.InputKey));

    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<CertificateElement> GenerationInputs => Elements
        .Where(e => !string.IsNullOrWhiteSpace(e.InputKey)).DistinctBy(e => e.InputKey);

    public void Validate(int pageCount)
    {
        if (Elements.Count == 0) throw new ArgumentException("Agrega al menos un componente al certificado.");
        foreach (var e in Elements)
        {
            if (e.PageIndex < 0 || e.PageIndex >= pageCount ||
                !double.IsFinite(e.X) || !double.IsFinite(e.Y) ||
                !double.IsFinite(e.Width) || !double.IsFinite(e.Height) ||
                e.X < 0 || e.Y < 0 || e.Width <= 0 || e.Height <= 0 ||
                e.X + e.Width > 1.000001 || e.Y + e.Height > 1.000001)
                throw new ArgumentException($"El componente '{e.Name}' debe estar dentro de una página válida.");
            if (!Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.Alignment) ||
                !double.IsFinite(e.FontSize) || e.FontSize < 6 || e.FontSize > 144 ||
                e.FontFamily is not ("Cinzel" or "Alex Brush") ||
                !System.Text.RegularExpressions.Regex.IsMatch(e.Color ?? "", "^#[0-9a-fA-F]{6}$"))
                throw new ArgumentException($"El formato de '{e.Name}' no es válido.");
            var unknown = System.Text.RegularExpressions.Regex.Matches(e.Content, @"\{[^{}]+\}")
                .Select(m => m.Value).Where(t => !Tokens.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
            if (e.Kind == CertificateElementKind.Text && string.IsNullOrWhiteSpace(e.InputKey) && unknown.Count > 0)
                throw new ArgumentException($"Variables desconocidas en '{e.Name}': {string.Join(", ", unknown)}.");
            if (!string.IsNullOrWhiteSpace(e.InputKey) && string.IsNullOrWhiteSpace(e.Name))
                throw new ArgumentException("Escribe un nombre para cada dato que se solicitará al generar.");
        }
        if (Elements.Where(e => !string.IsNullOrWhiteSpace(e.InputKey)).GroupBy(e => e.InputKey)
            .Any(group => group.Select(e => e.Kind).Distinct().Count() > 1))
            throw new ArgumentException("Un mismo dato no puede ser texto y firma a la vez.");
    }

    public static readonly string[] Tokens = ["{nombre}", "{cedula}", "{folio}", "{fecha}", "{tipo}", "{texto}", "{firmante}", "{cargo}"];

    public static CertificateLayout CreateDefault() => new()
    {
        Elements =
        [
            Element("Folio y fecha", "{folio}-{fecha}", 441, 51.7, 160, 18, 10.2, alignment: CertificateTextAlignment.Left),
            Element("Tipo de certificado", "{tipo}", 94, 96, 654, 57, 46, bold: true),
            Element("Nombre del participante", "{nombre}", 88, 195, 666, 58, 46.8, "Alex Brush", "#B0864F"),
            Element("Texto del certificado", "{texto}", 78, 258, 686, 100, 16),
            new() { Name = "Firma", Kind = CertificateElementKind.Signature, X = 526d / 842, Y = 384d / 595, Width = 218d / 842, Height = 98d / 595 },
            Element("Nombre del firmante", "{firmante}", 516, 488.3, 238, 21, 14.5, color: "#B0864F", bold: true),
            Element("Cargo", "{cargo}", 516, 512, 238, 22, 10)
        ]
    };

    private static CertificateElement Element(string name, string content, double x, double y, double w, double h,
        double size, string font = "Cinzel", string color = "#000000", bool bold = false,
        CertificateTextAlignment alignment = CertificateTextAlignment.Center) => new()
        { Name = name, Content = content, X = x / 842, Y = y / 595, Width = w / 842, Height = h / 595,
          FontSize = size, FontFamily = font, Color = color, Bold = bold, Alignment = alignment };
}

public enum CertificateElementKind { Text, Signature }
public enum CertificateTextAlignment { Left, Center, Right }

public sealed class CertificateElement
{
    // Empty uses the original fixed/template content. Otherwise this identifies a batch input.
    public string InputKey { get; set; } = string.Empty;
    public string Name { get; set; } = "Texto";
    public CertificateElementKind Kind { get; set; }
    public int PageIndex { get; set; }
    public string Content { get; set; } = "Texto personalizado";
    // Proportions of the page, independent of preview resolution and PDF size.
    public double X { get; set; } = .1;
    public double Y { get; set; } = .1;
    public double Width { get; set; } = .5;
    public double Height { get; set; } = .1;
    public string FontFamily { get; set; } = "Cinzel";
    public double FontSize { get; set; } = 16;
    public bool Bold { get; set; }
    public string Color { get; set; } = "#000000";
    public CertificateTextAlignment Alignment { get; set; } = CertificateTextAlignment.Center;
}

public sealed class CertificateTemplateSettings
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Mi plantilla";
    public string OriginalFileName { get; set; } = string.Empty;
    public required string TemplatePath { get; set; }
    public CertificateLayout Layout { get; set; } = CertificateLayout.CreateDefault();
}
