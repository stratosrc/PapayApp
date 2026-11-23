using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PapayApp.Core.Models;

namespace PapayApp.Infrastructure.Services;

public static class LaboratoryDocxTemplate
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly Regex Token = new(@"\{(?<key>[a-zA-Z_]+)(?:\|(?<fallback>[^{}]*))?\}", RegexOptions.Compiled);
    private static IEnumerable<ZipArchiveEntry> Parts(ZipArchive zip) => zip.Entries.Where(e =>
        e.FullName == "word/document.xml" || Regex.IsMatch(e.FullName, @"^word/(header|footer)\d+\.xml$"));
    private static List<(string Key, string Fallback)> Tokens(string path)
    {
        if (!Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("La plantilla debe ser un documento .docx.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("word/document.xml") is null) throw new InvalidDataException("El archivo no es un documento Word válido.");
        var tokens = new List<(string, string)>();
        foreach (var part in Parts(zip))
        {
            using var stream = part.Open();
            foreach (var p in XDocument.Load(stream).Descendants(W + "p"))
            {
                var text = string.Concat(TextNodes(p).Select(t => t.Value));
                tokens.AddRange(Token.Matches(text).Select(m => (m.Groups["key"].Value.ToLowerInvariant(), m.Groups["fallback"].Value)));
            }
        }
        return tokens;
    }
    public static void Validate(string path)
    {
        var tokens = Tokens(path);
        foreach (var key in new[] { "paciente", "cedula", "fecha" })
            if (!tokens.Any(t => t.Key == key)) throw new InvalidDataException($"Falta el marcador {{{key}}} en la plantilla. Insértalo en Word en su campo correspondiente.");
        var allowed = new[] { "paciente", "cedula", "edad", "genero", "sexo", "fecha" }.Concat(LaboratoryTestInterpreter.Analytes.Select(a => "resultado_" + a)).ToHashSet(StringComparer.Ordinal);
        var unknown = tokens.Where(t => !allowed.Contains(t.Key)).Select(t => t.Key).Distinct().ToList();
        if (unknown.Count > 0) throw new InvalidDataException("Marcadores desconocidos: " + string.Join(", ", unknown));
    }
    public static bool SupportsAutomatic(string path)
    {
        var tokens = Tokens(path);
        return LaboratoryTestInterpreter.Analytes.All(a => tokens.Any(t => t.Key == "resultado_" + a));
    }

    public static byte[] Fill(string path, LaboratoryPatient patient, DateTime date, IReadOnlyDictionary<string, string>? toxicology)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["paciente"] = patient.FullName, ["cedula"] = patient.Identification,
            ["edad"] = patient.Age.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["genero"] = patient.Sex, ["sexo"] = patient.Sex,
            ["fecha"] = date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
        };
        using var input = ZipFile.OpenRead(path);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var source in input.Entries)
            {
                var entry = zip.CreateEntry(source.FullName, CompressionLevel.Optimal);
                entry.LastWriteTime = source.LastWriteTime;
                using var src = source.Open(); using var dest = entry.Open();
                if (source.FullName != "word/document.xml" && !Regex.IsMatch(source.FullName, @"^word/(header|footer)\d+\.xml$")) { src.CopyTo(dest); continue; }
                var doc = XDocument.Load(src, LoadOptions.PreserveWhitespace);
                foreach (var p in doc.Descendants(W + "p").ToList())
                {
                    var text = string.Concat(TextNodes(p).Select(t => t.Value));
                    foreach (var match in Token.Matches(text).Reverse())
                    {
                        var key = match.Groups["key"].Value.ToLowerInvariant();
                        var isResult = key.StartsWith("resultado_", StringComparison.Ordinal);
                        var highlight = false;
                        string value;
                        if (isResult)
                        {
                            if (toxicology is not null && toxicology.TryGetValue(key[10..], out var result)) { value = result; highlight = true; }
                            else value = match.Groups["fallback"].Success ? match.Groups["fallback"].Value : "Negativo";
                        }
                        else if (!values.TryGetValue(key, out value!)) throw new InvalidDataException($"Marcador desconocido: {match.Value}");
                        Replace(p, match.Index, match.Length, value, highlight);
                    }
                }
                doc.Save(dest, SaveOptions.DisableFormatting);
            }
        }
        return output.ToArray();
    }

    private static List<XElement> TextNodes(XElement paragraph) => paragraph.Descendants(W + "t")
        .Where(t => ReferenceEquals(t.Ancestors(W + "p").FirstOrDefault(), paragraph)).ToList();

    // Replace across Word's split runs without flattening paragraphs, tables, drawings or styles.
    private static void Replace(XElement paragraph, int start, int length, string value, bool highlight)
    {
        var nodes = TextNodes(paragraph);
        var offset = 0;
        XElement? first = null;
        var prefix = ""; var suffix = "";
        foreach (var node in nodes)
        {
            var text = node.Value;
            var from = Math.Max(0, start - offset);
            var to = Math.Min(text.Length, start + length - offset);
            if (from < to)
            {
                if (first is null) { first = node; prefix = text[..from]; suffix = text[to..]; }
                node.Value = text[..from] + text[to..];
            }
            offset += text.Length;
        }
        if (first?.Parent is not { } run || run.Name != W + "r") throw new InvalidDataException("El marcador debe estar en un campo de texto normal de Word.");
        var trailing = first.NodesAfterSelf().ToList();
        first.Value = prefix;
        var inserted = new XElement(W + "r", run.Attributes().Select(a => new XAttribute(a)));
        var properties = run.Element(W + "rPr") is { } pr ? new XElement(pr) : new XElement(W + "rPr");
        if (highlight)
        {
            properties.Elements(W + "highlight").Remove();
            properties.Add(new XElement(W + "highlight", new XAttribute(W + "val", "yellow")));
        }
        inserted.Add(properties, new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value));
        run.AddAfterSelf(inserted);
        if (suffix.Length > 0 || trailing.Count > 0)
        {
            var after = new XElement(W + "r", run.Attributes().Select(a => new XAttribute(a)));
            if (run.Element(W + "rPr") is { } rpr) after.Add(new XElement(rpr));
            if (suffix.Length > 0) after.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), suffix));
            foreach (var node in trailing) { node.Remove(); after.Add(node); }
            inserted.AddAfterSelf(after);
        }
    }
}
