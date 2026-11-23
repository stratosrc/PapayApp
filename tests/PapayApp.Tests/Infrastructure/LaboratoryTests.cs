using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using PapayApp.Core.Models;
using PapayApp.Core.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class LaboratoryTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static string Templates => Path.Combine(TestPaths.RepositoryRoot, "src", "PapayApp.UI", "Resources", "Raw", "Templates");
    private static LaboratoryTemplateSettings Defaults => new()
    {
        ToxicologyPath = Path.Combine(Templates, "LAB_TOXICOLOGICOS.docx"), SerologyPath = Path.Combine(Templates, "LAB_SEROLOGICOS.docx")
    };
    private static LaboratoryGenerationRequest Request(string directory, string test = "REACTIVO - COC", bool automatic = true, bool overwrite = false) => new()
    {
        BaseDirectory = directory, Templates = Defaults, AutomaticToxicology = automatic, OverwriteConfirmed = overwrite,
        ReportDate = new(2026, 11, 22), Patients = [new(4, "Pérez González María Fernanda", "0912345678", 35, "F", test)]
    };
    private static XDocument ReadDocument(string path)
    {
        using var zip = ZipFile.OpenRead(path); using var stream = zip.GetEntry("word/document.xml")!.Open(); return XDocument.Load(stream);
    }
    private static XDocument ReadDocument(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }
    private sealed class RecordingConverter : IDocumentPdfConverter, IDocumentPdfSession
    {
        public List<byte[]> Documents { get; } = [];
        public int FailAt { get; set; } = -1;
        public bool Disposed { get; private set; }
        public IDocumentPdfSession OpenSession() => this;
        public byte[] Convert(byte[] document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Documents.Add(document);
            if (Documents.Count == FailAt) throw new IOException("Conversión interrumpida.");
            using var pdf = new PdfDocument(); pdf.AddPage();
            pdf.Info.Subject = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(document));
            using var output = new MemoryStream(); pdf.Save(output, false); return output.ToArray();
        }
        public void Dispose() => Disposed = true;
    }
    private static string Text(XContainer node) => string.Concat(node.Descendants(W + "t").Select(t => t.Value));

    [Theory]
    [InlineData("NO REACTIVO", "cocaina", "Negativo")]
    [InlineData("REACTIVO - COC", "cocaina", "Positivo")]
    [InlineData("REACTIVO - THC THC", "marihuana", "Positivo")]
    [InlineData("REACTIVO - BAR BAR", "barbituricos", "Positivo")]
    [InlineData("REACTIVO - AMP", "anfetaminas", "Positivo")]
    [InlineData("reactivo - COC / THC", "marihuana", "Positivo")]
    public void Interpreter_MapsOnlyConfirmedCodes(string test, string key, string expected)
    {
        var result = LaboratoryTestInterpreter.Interpret(test);
        Assert.NotNull(result); Assert.Equal(7, result.Count); Assert.Equal(expected, result[key]);
        Assert.Equal("Negativo", result["metadona"]);
    }
    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("TEST INVALIDO")]
    [InlineData("REACTIVO")]
    [InlineData("NO REACTIVO COC")]
    [InlineData("REACTIVO - XYZ")]
    public void Interpreter_DoesNotGuessMissingOrUnknownResults(string test) => Assert.Null(LaboratoryTestInterpreter.Interpret(test));

    [Fact]
    public async Task Import_DetectsOffsetHeadersAnyColumnOrderAndLeadingZeros()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(root.FullName, "matrix.xlsx");
            using (var book = new XLWorkbook())
            {
                var sheet = book.AddWorksheet("Pacientes"); book.AddWorksheet("Resumen");
                sheet.Cell("A1").Value = "Matriz";
                string[] headers = ["GÉNERO", " EDAD ", "CÉDULA", "NOMBRES Y APELLIDOS", "TEST"];
                for (var i = 0; i < headers.Length; i++) sheet.Cell(3, i + 1).Value = headers[i];
                sheet.Cell("A4").Value = "f"; sheet.Cell("B4").Value = 35; sheet.Cell("C4").Value = 912345678;
                sheet.Cell("D4").Value = "María Pérez"; sheet.Cell("E4").Value = "REACTIVO - COC";
                sheet.Cell("A5").Value = "M"; sheet.Cell("B5").Value = 40; sheet.Cell("C5").Value = "0912345678"; sheet.Cell("D5").Value = "Duplicado";
                sheet.Cell("A6").Value = "?"; sheet.Cell("B6").Value = -1; sheet.Cell("D6").Value = "Datos incompletos";
                book.SaveAs(path);
            }
            var service = new LaboratoryImportService();
            Assert.Equal(["Pacientes"], await service.GetWorksheetNamesAsync(path));
            var imported = await service.ImportAsync(path, "Pacientes");
            var patient = Assert.Single(imported.Patients);
            Assert.Equal("0912345678", patient.Identification); Assert.Equal("FEMENINO", patient.Sex); Assert.Equal(4, patient.RowNumber);
            Assert.Equal(2, imported.Issues.Count);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Generation_FillsBothDocumentsAndHighlightsOnlyResultColumn()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var converter = new RecordingConverter();
            var result = await new LaboratoryGenerationService(new SafeFileNameService(), converter).GenerateAsync(Request(root.FullName));
            var output = Assert.Single(result.Files); Assert.True(output.Success, output.Error);
            var patientFolder = Path.Combine(root.FullName, "PRUEBAS", "22112026", "Pérez González María Fernanda");
            Assert.Equal(Path.Combine(patientFolder, "RESULTADO TOXICOLÓGICO EN ORINA -  PÉREZ GONZÁLEZ MARÍA FERNANDA.pdf"), output.ToxicologyPath);
            Assert.Equal(Path.Combine(patientFolder, "RESULTADO SEROLÓGICO DE VIH -  PÉREZ GONZÁLEZ MARÍA FERNANDA.pdf"), output.SerologyPath);
            Assert.True(converter.Disposed);
            Assert.Empty(Directory.GetFiles(root.FullName, "*.docx", SearchOption.AllDirectories));
            using var actualPdf = PdfReader.Open(output.ToxicologyPath!, PdfDocumentOpenMode.Import);
            Assert.Equal(1, actualPdf.PageCount);
            using var serologyPdf = PdfReader.Open(output.SerologyPath!, PdfDocumentOpenMode.Import);
            Assert.Equal(1, serologyPdf.PageCount);
            var toxicology = ReadDocument(converter.Documents[0]);
            Assert.Contains("0912345678", Text(toxicology)); Assert.Contains("22/11/2026", Text(toxicology)); Assert.DoesNotContain("{", Text(toxicology));
            var rows = toxicology.Descendants(W + "tbl").ElementAt(1).Elements(W + "tr").Skip(1).ToList();
            foreach (var row in rows)
            {
                var cells = row.Elements(W + "tc").ToList();
                var expected = Text(cells[0]) == "Cocaína" ? "Positivo" : "Negativo";
                Assert.Equal(expected, Text(cells[1]));
                Assert.Single(cells[1].Descendants(W + "highlight"));
                Assert.Equal("Negativo", Text(cells[2])); Assert.Empty(cells[2].Descendants(W + "highlight"));
            }
            var serology = ReadDocument(converter.Documents[1]);
            Assert.Contains("22/11/2026", Text(serology));
            Assert.Contains("FEMENINO", Text(serology)); Assert.Contains("35", Text(serology));
            Assert.Equal(2, serology.Descendants(W + "t").Count(t => t.Value == "NO REACTIVO"));
            Assert.Empty(serology.Descendants(W + "highlight"));
            // Logos, signatures and all package parts outside document.xml remain byte-for-byte unchanged.
            using var source = ZipFile.OpenRead(Defaults.ToxicologyPath); using var generated = new ZipArchive(new MemoryStream(converter.Documents[0]));
            foreach (var part in source.Entries.Where(e => e.FullName != "word/document.xml"))
            {
                using var a = part.Open(); using var b = generated.GetEntry(part.FullName)!.Open();
                using var bytesA = new MemoryStream(); using var bytesB = new MemoryStream(); a.CopyTo(bytesA); b.CopyTo(bytesB);
                Assert.Equal(bytesA.ToArray(), bytesB.ToArray());
            }
        }
        finally { root.Delete(true); }
    }

    [Theory]
    [InlineData(true, "TEST INVALIDO", true)]
    [InlineData(true, "", true)]
    [InlineData(false, "REACTIVO - COC", false)]
    public async Task Generation_PreservesDefaultsForManualAndInvalidResults(bool automatic, string test, bool warning)
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var converter = new RecordingConverter();
            var result = await new LaboratoryGenerationService(new SafeFileNameService(), converter).GenerateAsync(Request(root.FullName, test, automatic));
            var item = Assert.Single(result.Files); Assert.True(item.Success, item.Error);
            var document = ReadDocument(converter.Documents[0]);
            Assert.DoesNotContain("Positivo", Text(document)); Assert.Empty(document.Descendants(W + "highlight"));
            Assert.Equal(warning, result.Warnings.Count > 0); Assert.Equal(warning, result.LogPath is not null);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Generation_RequiresOverwriteConfirmationAndPreservesFilesWhenLocked()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var converter = new RecordingConverter();
            var service = new LaboratoryGenerationService(new SafeFileNameService(), converter);
            var initial = Assert.Single((await service.GenerateAsync(Request(root.FullName))).Files);
            var before = File.ReadAllBytes(initial.ToxicologyPath!);
            Assert.Equal(2, service.ExistingFiles(Request(root.FullName)).Count);
            await Assert.ThrowsAsync<IOException>(() => service.GenerateAsync(Request(root.FullName)));
            Assert.Equal(before, File.ReadAllBytes(initial.ToxicologyPath!));
            using (var locked = new FileStream(initial.SerologyPath!, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var failed = await service.GenerateAsync(Request(root.FullName, "NO REACTIVO", overwrite: true));
                Assert.False(Assert.Single(failed.Files).Success);
                Assert.Equal(before, File.ReadAllBytes(initial.ToxicologyPath!));
            }
            var overwritten = await service.GenerateAsync(Request(root.FullName, "NO REACTIVO", overwrite: true));
            Assert.True(Assert.Single(overwritten.Files).Success);
            Assert.DoesNotContain("Positivo", Text(ReadDocument(converter.Documents[^2])));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task ConversionFailure_PreservesExistingPdfPair()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var converter = new RecordingConverter();
            var service = new LaboratoryGenerationService(new SafeFileNameService(), converter);
            var initial = Assert.Single((await service.GenerateAsync(Request(root.FullName))).Files);
            var toxicology = File.ReadAllBytes(initial.ToxicologyPath!);
            var serology = File.ReadAllBytes(initial.SerologyPath!);
            converter.FailAt = 4; // The second document in the replacement batch fails conversion.
            var result = await service.GenerateAsync(Request(root.FullName, "NO REACTIVO", overwrite: true));
            Assert.False(Assert.Single(result.Files).Success);
            Assert.Equal(toxicology, File.ReadAllBytes(initial.ToxicologyPath!));
            Assert.Equal(serology, File.ReadAllBytes(initial.SerologyPath!));
            Assert.Empty(Directory.GetFiles(root.FullName, "*.docx", SearchOption.AllDirectories));
            Assert.True(converter.Disposed);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Templates_SaveCopiesAndDoesNotActivateInvalidOrUnsavedChanges()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var settingsDirectory = Path.Combine(root.FullName, "settings");
            var store = new JsonLaboratoryTemplateStore(settingsDirectory, Templates);
            var draft = store.Load();
            Assert.False(Directory.Exists(settingsDirectory));
            store.Save(draft);
            var saved = store.Load(); Assert.NotEqual(draft.ToxicologyPath, saved.ToxicologyPath);
            Assert.Equal(File.ReadAllBytes(draft.ToxicologyPath), File.ReadAllBytes(saved.ToxicologyPath));
            draft.ToxicologyPath = Path.Combine(root.FullName, "missing.docx");
            Assert.Throws<FileNotFoundException>(() => store.Save(draft));
            Assert.Equal(saved.ToxicologyPath, store.Load().ToxicologyPath);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void TemplateReplacement_HandlesSplitRunsWithoutLosingSurroundingText()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var template = Path.Combine(root.FullName, "split.docx");
            using (var zip = ZipFile.Open(template, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                writer.Write("<w:document xmlns:w='" + W + "'><w:body><w:p><w:r><w:t>Antes {pa</w:t></w:r><w:r><w:t>ciente} después {cedula} {fecha}</w:t></w:r></w:p></w:body></w:document>");
            LaboratoryDocxTemplate.Validate(template);
            var output = Path.Combine(root.FullName, "filled.docx");
            File.WriteAllBytes(output, LaboratoryDocxTemplate.Fill(template, new(1, "Ana Pérez", "0912345678", 30, "F", ""), new DateTime(2026, 11, 22), null));
            Assert.Equal("Antes Ana Pérez después 0912345678 22/11/2026", Text(ReadDocument(output)));
        }
        finally { root.Delete(true); }
    }
}
