using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class CertificateGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_CreatesSeparateFilesAndAdvancesFromLastUsedFolio()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = new CertificateGenerationService(new SafeFileNameService());
            var result = await service.GenerateAsync(CreateRequest(
                outputDirectory.FullName,
                CertificateOutputMode.SeparateFiles,
                [
                    new CertificateParticipant(2, "Melanie Parra Pibaque", "0953456423"),
                    new CertificateParticipant(3, "Juan Pérez", "0912345678")
                ]));

            Assert.Equal(2, result.SucceededCount);
            Assert.Equal(1031, result.LastUsedFolio);
            Assert.Equal(1032, result.NextFolio);
            Assert.Collection(
                result.Results.OrderBy(item => item.Folio),
                first =>
                {
                    Assert.Equal(1030, first.Folio);
                    Assert.EndsWith("1030_Melanie Parra Pibaque.pdf", first.OutputPath, StringComparison.OrdinalIgnoreCase);
                },
                second =>
                {
                    Assert.Equal(1031, second.Folio);
                    Assert.EndsWith("1031_Juan Pérez.pdf", second.OutputPath, StringComparison.OrdinalIgnoreCase);
                });
            Assert.All(result.Results, item => Assert.True(File.Exists(item.OutputPath)));
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_CreatesCombinedPdfInParticipantOrder()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = new CertificateGenerationService(new SafeFileNameService());
            var result = await service.GenerateAsync(CreateRequest(
                outputDirectory.FullName,
                CertificateOutputMode.CombinedPdf,
                [
                    new CertificateParticipant(2, "Melanie Parra Pibaque", "0953456423"),
                    new CertificateParticipant(3, "Juan Pérez", "0912345678")
                ]));

            Assert.Equal(2, result.SucceededCount);
            Assert.NotNull(result.CombinedPdfPath);
            Assert.EndsWith("Certificados_1030-1031.pdf", result.CombinedPdfPath, StringComparison.OrdinalIgnoreCase);

            using var pdf = PdfReader.Open(result.CombinedPdfPath, PdfDocumentOpenMode.Import);
            Assert.Equal(2, pdf.PageCount);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_ConsumesFailedFoliosAndWritesLog()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = new CertificateGenerationService(new SafeFileNameService());
            var request = CreateRequest(
                outputDirectory.FullName,
                CertificateOutputMode.SeparateFiles,
                [new CertificateParticipant(2, new string('N', 1000), "0953456423")]);

            var result = await service.GenerateAsync(request);

            Assert.Equal(0, result.SucceededCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(1030, result.LastUsedFolio);
            Assert.Equal(1031, result.NextFolio);
            Assert.NotNull(result.LogPath);
            Assert.Contains("Folio 1030", File.ReadAllText(result.LogPath));
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    private static CertificateGenerationRequest CreateRequest(
        string outputDirectory,
        CertificateOutputMode outputMode,
        IReadOnlyList<CertificateParticipant> participants)
    {
        return new CertificateGenerationRequest
        {
            TemplatePath = TestPaths.CertificateTemplatePath,
            AlexBrushFontPath = TestPaths.AlexBrushFontPath,
            CinzelRegularFontPath = TestPaths.CinzelRegularFontPath,
            CinzelBoldFontPath = TestPaths.CinzelBoldFontPath,
            OutputDirectory = outputDirectory,
            SignaturePngPath = TestPaths.DiplomaIconPath,
            CertificateType = "PARTICIPACIÓN",
            BodyTemplate = "CON CÉDULA {cedula} POR APROBAR EL CURSO DE PRUEBA.",
            SignerName = "DRA. PERSONA FIRMANTE",
            SignerTitle = "COORDINACIÓN ACADÉMICA",
            Participants = participants,
            OutputMode = outputMode,
            CertificateDate = new DateTime(2026, 8, 26),
            LastUsedFolio = 1029
        };
    }
}
