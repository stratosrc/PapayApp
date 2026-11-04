using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class CertificateTemplateTests
{
    [Fact]
    public async Task Generation_UsesIndependentSignerInputsAndRequiresTheirOwnFiles()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var layout = new CertificateLayout { Elements =
            [
                new() { Name = "Nombre firmante 1", InputKey = "name-1", Content = "{firmante}" },
                new() { Name = "Nombre firmante 2", InputKey = "name-2", Y = .3 },
                new() { Name = "Cargo firmante 2", InputKey = "title-2", Y = .5 },
                new() { Name = "Firma 1", Kind = CertificateElementKind.Signature, InputKey = "signature-1", Y = .7, Width = .2 },
                new() { Name = "Firma 2", Kind = CertificateElementKind.Signature, InputKey = "signature-2", X = .5, Y = .7, Width = .2 }
            ] };
            Assert.False(layout.RequiresSignature);
            Assert.False(layout.Uses("{firmante}"));
            var fields = new Dictionary<string, string>
            {
                ["name-1"] = "Ana Pérez", ["name-2"] = "Juan López", ["title-2"] = "Director\nCédula: {cedula}",
                ["signature-1"] = TestPaths.DiplomaIconPath,
                ["signature-2"] = Path.Combine(TestPaths.RepositoryRoot, "src", "PapayApp.UI", "Resources", "Images", "Navigation", "home.png")
            };
            var service = new CertificateGenerationService(new SafeFileNameService());
            var result = await service.GenerateAsync(Request(source, root.FullName, layout, false, fields));
            Assert.Equal(2, result.SucceededCount);
            fields["signature-2"] = Path.Combine(root.FullName, "missing.png");
            var missingFile = await Assert.ThrowsAsync<FileNotFoundException>(() => service.GenerateAsync(Request(source, root.FullName, layout, false, fields)));
            Assert.Contains("Firma 2", missingFile.Message);
            fields.Remove("name-2");
            var missingText = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(Request(source, root.FullName, layout, false, fields)));
            Assert.Contains("Nombre firmante 2", missingText.Message);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Store_PreservesGenerationFieldIdentityAndLabelsPerPreset()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var store = new JsonCertificateTemplateStore(Path.Combine(root.FullName, "settings"), source);
            store.Save(new() { Name = "Dos firmantes", TemplatePath = source, Layout = new() { Elements =
                [new() { Name = "Segundo cargo", InputKey = "second-title" }] } });
            var saved = store.Load();
            Assert.Equal("second-title", Assert.Single(saved.Layout.GenerationInputs).InputKey);
            Assert.Equal("Segundo cargo", saved.Layout.Elements[0].Name);
            Assert.Empty(store.List().Single(t => t.Id == "original").Layout.GenerationInputs);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Library_KeepsIndependentPresetsAndOnlyCommitsOnSave()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var directory = Path.Combine(root.FullName, "settings");
            var store = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath);
            var a = new CertificateTemplateSettings { Name = "Participación", TemplatePath = source,
                Layout = new() { Elements = [new() { Content = "Plantilla A", X = .15 }] } };
            var b = new CertificateTemplateSettings { Name = "Aprobación", TemplatePath = source,
                Layout = new() { Elements = [new() { Content = "Plantilla B", X = .35, FontSize = 24 }] } };
            store.Save(a);
            store.Save(b);
            Assert.NotEqual(a.Id, b.Id);
            Assert.Equal(3, store.List().Count);
            Assert.Equal(b.Id, store.Load().Id);
            var committed = File.ReadAllText(Path.Combine(directory, "certificate-templates.json"));
            var edited = store.List().Single(t => t.Id == a.Id);
            edited.Name = "Cambio pendiente";
            edited.Layout.Elements[0].X = .5;
            // Reading, selecting and editing snapshots neither activates nor persists anything.
            Assert.Equal(b.Id, store.Load().Id);
            Assert.Equal(.15, store.List().Single(t => t.Id == a.Id).Layout.Elements[0].X);
            Assert.Equal(committed, File.ReadAllText(Path.Combine(directory, "certificate-templates.json")));
            store.Save(edited);
            Assert.Equal(3, store.List().Count);
            var reopened = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath);
            Assert.Equal(a.Id, reopened.Load().Id);
            Assert.Equal(.5, reopened.Load().Layout.Elements[0].X);
            Assert.Equal("Cambio pendiente", reopened.Load().Name);
            var other = reopened.List().Single(t => t.Id == b.Id);
            Assert.Equal(.35, other.Layout.Elements[0].X);
            Assert.Equal(24, other.Layout.Elements[0].FontSize);
            edited.Layout.Elements[0].Width = 4;
            Assert.Throws<ArgumentException>(() => reopened.Save(edited));
            Assert.Equal(.5, reopened.Load().Layout.Elements[0].X);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Library_ReadsAndMigratesLegacyTemplateOnlyWhenSaved()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var directory = Path.Combine(root.FullName, "settings");
            var store = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath);
            Assert.Single(store.List());
            store.Load();
            Assert.False(Directory.Exists(directory));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "certificate-template.json"),
                System.Text.Json.JsonSerializer.Serialize(new CertificateTemplateSettings
                { TemplatePath = source, Layout = new() { Elements = [new() { Content = "Diseño anterior" }] } }));
            Assert.Equal("Diseño anterior", store.Load().Layout.Elements[0].Content);
            Assert.Equal(2, store.List().Count);
            Assert.False(File.Exists(Path.Combine(directory, "certificate-templates.json")));
            store.Save(store.Load());
            File.Delete(source);
            var migrated = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath);
            Assert.Equal(2, migrated.List().Count);
            Assert.Equal("Diseño anterior", migrated.Load().Layout.Elements[0].Content);
            Assert.True(File.Exists(migrated.Load().TemplatePath));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Store_CopiesPdfAndPersistsCustomLayoutAcrossInstances()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var directory = Path.Combine(root.FullName, "settings");
            var store = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath);
            Assert.Equal(7, store.Load().Layout.Elements.Count);
            var layout = new CertificateLayout { Elements = [new() { Name = "Institución", Content = "Academia", PageIndex = 1, X = .23 }] };
            store.Save(new() { TemplatePath = source, Layout = layout });
            File.Delete(source);
            var saved = new JsonCertificateTemplateStore(directory, TestPaths.CertificateTemplatePath).Load();
            Assert.True(File.Exists(saved.TemplatePath));
            var field = Assert.Single(saved.Layout.Elements);
            Assert.Equal(.23, field.X);
            Assert.Equal(1, field.PageIndex);
            Assert.Equal("Academia", field.Content);

            // Invalid drafts cannot replace the active settings.
            field.Width = 2;
            Assert.Throws<ArgumentException>(() => store.Save(saved));
            Assert.Equal(.5, store.Load().Layout.Elements[0].Width);
        }
        finally { root.Delete(true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_PreservesAllPagesAndUsesCustomLayoutWithoutSignature(bool combined)
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var layout = new CertificateLayout { Elements =
            [
                new() { Content = "Institución: Academia", X = .2, Y = .3, Width = .6, Height = .2 },
                new() { PageIndex = 1, Content = "{nombre} | {cedula} | {folio} | {fecha}", Width = .8, Height = .3 }
            ] };
            var service = new CertificateGenerationService(new SafeFileNameService());
            var result = await service.GenerateAsync(Request(source, root.FullName, layout, combined));
            Assert.Equal(2, result.SucceededCount);
            Assert.Equal(0, result.FailedCount);
            foreach (var path in result.Results.Select(r => r.OutputPath).Distinct())
            {
                using var document = PdfReader.Open(path!, PdfDocumentOpenMode.Import);
                Assert.Equal(combined ? 4 : 2, document.PageCount);
                Assert.Equal(420, document.Pages[0].Width.Point, 1);
                Assert.Equal(600, document.Pages[1].Width.Point, 1);
            }
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Generation_RejectsTextThatCannotFitInConfiguredHeight()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var source = Path.Combine(root.FullName, "source.pdf");
            CreatePdf(source);
            var layout = new CertificateLayout { Elements = [new() { Content = "Texto", Height = .001 }] };
            var result = await new CertificateGenerationService(new SafeFileNameService())
                .GenerateAsync(Request(source, root.FullName, layout, false));
            Assert.Equal(2, result.FailedCount);
            Assert.All(result.Results, r => Assert.Contains("no cabe", r.ErrorMessage));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Layout_RejectsInvalidPageCoordinatesAndVariables()
    {
        var layout = new CertificateLayout { Elements = [new() { PageIndex = 2 }] };
        Assert.Throws<ArgumentException>(() => layout.Validate(2));
        layout.Elements[0].PageIndex = 0;
        layout.Elements[0].X = double.NaN;
        Assert.Throws<ArgumentException>(() => layout.Validate(2));
        layout.Elements[0].X = .1;
        layout.Elements[0].Content = "{desconocido}";
        Assert.Throws<ArgumentException>(() => layout.Validate(2));
        layout.Elements[0].Content = "{NOMBRE} {cedula}";
        layout.Validate(2);
    }

    private static void CreatePdf(string path)
    {
        using var document = new PdfDocument();
        var first = document.AddPage(); first.Width = PdfSharp.Drawing.XUnit.FromPoint(420); first.Height = PdfSharp.Drawing.XUnit.FromPoint(600);
        var second = document.AddPage(); second.Width = PdfSharp.Drawing.XUnit.FromPoint(600); second.Height = PdfSharp.Drawing.XUnit.FromPoint(420);
        document.Save(path);
    }

    private static CertificateGenerationRequest Request(string template, string output, CertificateLayout layout, bool combined,
        IReadOnlyDictionary<string, string>? fields = null) => new()
    {
        FieldValues = fields ?? new Dictionary<string, string>(),
        TemplatePath = template, Layout = layout, OutputDirectory = output,
        AlexBrushFontPath = TestPaths.AlexBrushFontPath,
        CinzelRegularFontPath = TestPaths.CinzelRegularFontPath,
        CinzelBoldFontPath = TestPaths.CinzelBoldFontPath,
        SignaturePngPath = "", CertificateType = "", BodyTemplate = "", SignerName = "", SignerTitle = "",
        CertificateDate = new DateTime(2026, 11, 3),
        Participants = [new(1, "Ana Pérez", "0912345678"), new(2, "José López", "0953456423")],
        OutputMode = combined ? CertificateOutputMode.CombinedPdf : CertificateOutputMode.SeparateFiles
    };
}
