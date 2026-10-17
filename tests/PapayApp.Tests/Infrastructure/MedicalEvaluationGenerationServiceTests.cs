using System.Security.Cryptography;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class MedicalEvaluationGenerationServiceTests
{
    [Theory]
    [InlineData(null, null, "RIOCENTRO QUITO", "RIOCENTRO QUITO")]
    [InlineData("OFICIO:", "AUXILIAR DE LIMPIEZA (9112)", "RIOCENTRO QUITO", "AUXILIAR DE LIMPIEZA (9112) / RIOCENTRO QUITO")]
    [InlineData("Oficio", "TECNICO", "RIOCENTRO QUITO", "TECNICO / RIOCENTRO QUITO")]
    [InlineData("OFICIO:", "", "RIOCENTRO QUITO", "RIOCENTRO QUITO")]
    [InlineData("OFICIO:", "AUXILIAR DE LIMPIEZA (9112)", "", "AUXILIAR DE LIMPIEZA (9112)")]
    [InlineData(null, null, "", "")]
    public async Task ImportedJobTitle_IsWrittenToBothEvaluationSheets(
        string? header, string? oficio, string centroCosto, string expectedTitle)
    {
        using var files = new TemporaryTemplateFiles();
        var masterPath = Path.ChangeExtension(files.Output, ".master.xlsx");
        using (var master = new XLWorkbook())
        {
            var sheet = master.AddWorksheet("MAESTRO");
            sheet.Cell("A1").Value = "Cedula";
            sheet.Cell("B1").Value = "Apellidos";
            sheet.Cell("C1").Value = "Nombres";
            sheet.Cell("D1").Value = "Centro de Costo";
            sheet.Cell("A2").Value = "0123456789";
            sheet.Cell("B2").Value = "MOLINA TORRES";
            sheet.Cell("C2").Value = "ROSALINDA ELIZABETH";
            sheet.Cell("D2").Value = centroCosto;
            if (header is not null)
            {
                sheet.Cell("E1").Value = header;
                sheet.Cell("E2").Value = oficio ?? string.Empty;
            }
            master.SaveAs(masterPath);
        }
        var importer = new ExcelImportService(new HeaderNormalizer(), new NameSplitService());
        var preview = await importer.ImportWorksheetAsync(masterPath, "MAESTRO", CancellationToken.None);
        var imported = Assert.Single(preview.Records);
        Assert.Equal(header is not null, preview.Headers.Any(h => h.Field == EmployeeField.Oficio));
        var selection = new EmployeeGenerationSelection
        {
            Record = imported.Record,
            NameParts = imported.NameParts,
            Gender = EmployeeGender.Masculino,
            RowNumber = imported.RowNumber
        };
        new MedicalTemplateWriter(new AgeCalculationService()).Write(files.Template, files.Output,
            selection, OccupationalEvaluationType.Periodica, DateTime.Today, null, CreateCompany(), null);
        using var output = new XLWorkbook(files.Output);
        Assert.Equal(expectedTitle, output.Worksheet("-EVALUACION OCUPACIONAL 1-3").Cell("J16").GetString());
        Assert.Equal(expectedTitle, output.Worksheet("EVALUACION OCUPACIONAL2-3").Cell("G2").GetString());
    }

    [Fact]
    public void PeriodicTemplate_PreservesTextFormattingAndThirdSheetLayout()
    {
        using var directory = new TemporaryTemplateFiles();
        var writer = new MedicalTemplateWriter(new AgeCalculationService());
        writer.Write(directory.Template, directory.Output, CreateSelection("0123456789"),
            OccupationalEvaluationType.Periodica, DateTime.Today, null, CreateCompany(), null);
        using var expected = new XLWorkbook(directory.Template);
        using var actual = new XLWorkbook(directory.Output);
        var description = actual.Worksheet("-EVALUACION OCUPACIONAL 1-3").Cell("B49");
        Assert.Equal(12d, description.Style.Font.FontSize);
        Assert.All(description.GetRichText(), run => Assert.Equal(12d, run.FontSize));
        foreach (var sheet in expected.Worksheets)
        {
            var result = actual.Worksheet(sheet.Name);
            foreach (var cell in sheet.CellsUsed().Where(c => c.HasRichText))
            {
                Assert.Equal(cell.GetRichText().Select(r => (r.Text, r.FontSize, r.FontName, r.Bold)),
                    result.Cell(cell.Address).GetRichText().Select(r => (r.Text, r.FontSize, r.FontName, r.Bold)));
            }
        }
        var third = expected.Worksheet("EVALUACION OCUPACIONAL 3-3");
        var outputThird = actual.Worksheet(third.Name);
        AssertSheetValuesAndFormulasEqual(third, outputThird);
        Assert.Equal(third.MergedRanges.Select(r => r.RangeAddress.ToString()).Order(),
            outputThird.MergedRanges.Select(r => r.RangeAddress.ToString()).Order());
        foreach (var row in third.RowsUsed())
        {
            Assert.Equal(row.Height, outputThird.Row(row.RowNumber()).Height);
            Assert.Equal(row.IsHidden, outputThird.Row(row.RowNumber()).IsHidden);
        }
        foreach (var cell in third.CellsUsed(XLCellsUsedOptions.All))
            Assert.Equal(cell.Style, outputThird.Cell(cell.Address).Style);
    }

    private sealed class TemporaryTemplateFiles : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory();
        public string Template => Path.Combine(_directory.FullName, "template.xlsx");
        public string Output => Path.Combine(_directory.FullName, "output.xlsx");
        public TemporaryTemplateFiles()
        {
            using var source = File.Open(TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Periodica),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var destination = File.Create(Template);
            source.CopyTo(destination);
        }
        public void Dispose() => _directory.Delete(true);
    }

    [Theory]
    [InlineData(OccupationalEvaluationType.Periodica, "Z19", "EV PERIODICA - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Preocupacional, "G19", "EV INICIAL - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Reintegro, "AI19", "EV REINTEGRO - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Retiro, "BA19", "EV RETIRO - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    public async Task GenerateAsync_UsesCorrectTemplateAndOnlyUpdatesAllowedCells(
        OccupationalEvaluationType evaluationType,
        string evaluationMarkCell,
        string expectedFileName)
    {
        var templatePath = TestPaths.MedicalTemplatePath(evaluationType);
        var originalTemplateHash = ComputeSha256(templatePath);
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var summary = await service.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = templatePath,
                    OutputBaseDirectory = outputDirectory.FullName,
                    EvaluationType = evaluationType,
                    GeneratedAt = new DateTime(2026, 7, 10),
                    AttentionDate = new DateTime(2026, 7, 10),
                    Company = CreateCompany(),
                    Records = [CreateSelection("0123456789", "RIOCENTRO QUITO")]
                },
                CancellationToken.None);

            var result = Assert.Single(summary.Results);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.OutputPath);
            Assert.EndsWith(expectedFileName, result.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.OutputPath));

            using var templateWorkbook = new XLWorkbook(templatePath);
            using var workbook = new XLWorkbook(result.OutputPath);
            var evaluationSheet = workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3");
            var occupationalSheet = workbook.Worksheet("EVALUACION OCUPACIONAL2-3");
            var thirdSheet = workbook.Worksheet("EVALUACION OCUPACIONAL 3-3");
            var certificateSheet = workbook.Worksheet("CERTIFICADO");

            Assert.Equal("MANLIM S.A.S", evaluationSheet.Cell("B5").GetString());
            Assert.Equal("0992424362001", evaluationSheet.Cell("Q5").GetString());
            Assert.Equal("@", evaluationSheet.Cell("Q5").Style.NumberFormat.Format);
            Assert.Equal("N8110.00", evaluationSheet.Cell("Z5").GetString());
            Assert.Equal("0123456789", evaluationSheet.Cell("AP5").GetString());
            Assert.Equal("MOLINA", evaluationSheet.Cell("B8").GetString());
            Assert.Equal("TORRES", evaluationSheet.Cell("S8").GetString());
            Assert.Equal("ROSALINDA", evaluationSheet.Cell("AH8").GetString());
            Assert.Equal("ELIZABETH", evaluationSheet.Cell("AR8").GetString());
            Assert.Equal("X", evaluationSheet.Cell("U13").GetString());
            Assert.Equal("AUXILIAR DE LIMPIEZA (9112) / RIOCENTRO QUITO", evaluationSheet.Cell("J16").GetString());
            Assert.Equal(new DateTime(2026, 7, 10), evaluationSheet.Cell("AH16").GetDateTime());
            Assert.Equal("dd/MM/yyyy", evaluationSheet.Cell("AH16").Style.DateFormat.Format);
            Assert.Equal("X", evaluationSheet.Cell(evaluationMarkCell).GetString());

            Assert.Equal("AUXILIAR DE LIMPIEZA (9112) / RIOCENTRO QUITO", occupationalSheet.Cell("G2").GetString());

            var originalThirdSheet = templateWorkbook.Worksheet("EVALUACION OCUPACIONAL 3-3");
            AssertSheetValuesAndFormulasEqual(originalThirdSheet, thirdSheet);

            var originalCertificateSheet = templateWorkbook.Worksheet("CERTIFICADO");
            AssertFormulaCellsUnchanged(originalCertificateSheet, certificateSheet);
            Assert.Equal(originalCertificateSheet.Pictures.Count, certificateSheet.Pictures.Count);
            Assert.Contains("B5", certificateSheet.Cell("A4").FormulaA1, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Q5", certificateSheet.Cell("L4").FormulaA1, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Z5", certificateSheet.Cell("R4").FormulaA1, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(XLCalculateMode.Auto, workbook.CalculateMode);

            Assert.Equal(originalTemplateHash, ComputeSha256(templatePath));
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_LeavesAttentionDateBlankWhenNotSelected()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var summary = await service.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Preocupacional),
                    OutputBaseDirectory = outputDirectory.FullName,
                    EvaluationType = OccupationalEvaluationType.Preocupacional,
                    GeneratedAt = new DateTime(2026, 10, 6),
                    AttentionDate = null,
                    Company = CreateCompany(),
                    Records = [CreateSelection("0123456789", "MALL DEL SOL")]
                },
                CancellationToken.None);

            var result = Assert.Single(summary.Results);
            Assert.True(result.Success, result.ErrorMessage);
            using var workbook = new XLWorkbook(result.OutputPath!);
            Assert.True(workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3").Cell("AH16").IsEmpty());
            Assert.Contains("Fecha de atencion: En blanco", File.ReadAllText(summary.LogPath!), StringComparison.Ordinal);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_InsertsCenteredLogoInEvaluationAndCertificate()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var summary = await service.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Preocupacional),
                    OutputBaseDirectory = outputDirectory.FullName,
                    EvaluationType = OccupationalEvaluationType.Preocupacional,
                    GeneratedAt = new DateTime(2026, 7, 10),
                    AttentionDate = new DateTime(2026, 7, 10),
                    Company = CreateCompany(),
                    LogoPath = TestPaths.DiplomaIconPath,
                    Records = [CreateSelection("0123456789", "MALL DEL SOL")]
                },
                CancellationToken.None);

            var result = Assert.Single(summary.Results);
            Assert.True(result.Success, result.ErrorMessage);
            using var workbook = new XLWorkbook(result.OutputPath!);
            var evaluationPicture = Assert.Single(workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3").Pictures);
            var certificatePicture = Assert.Single(workbook.Worksheet("CERTIFICADO").Pictures);

            Assert.Equal(XLPicturePlacement.MoveAndSize, evaluationPicture.Placement);
            Assert.Equal(XLPicturePlacement.MoveAndSize, certificatePicture.Placement);
            AssertPictureIsAnchoredInside(evaluationPicture, 1, 2, 1, 11);
            AssertPictureIsAnchoredInside(certificatePicture, 1, 1, 1, 8);
            Assert.True(evaluationPicture.Width > 0);
            Assert.True(evaluationPicture.Height > 0);
            Assert.True(certificatePicture.Width > 0);
            Assert.True(certificatePicture.Height > 0);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_AcceptsJpegLogo()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();
        var jpegPath = Path.Combine(outputDirectory.FullName, "logo.jpg");

        try
        {
            File.WriteAllBytes(jpegPath, TestPaths.TinyJpegBytes);
            var service = CreateGenerationService();
            var summary = await service.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Periodica),
                    OutputBaseDirectory = outputDirectory.FullName,
                    EvaluationType = OccupationalEvaluationType.Periodica,
                    GeneratedAt = new DateTime(2026, 10, 6),
                    AttentionDate = new DateTime(2026, 10, 6),
                    Company = CreateCompany(),
                    LogoPath = jpegPath,
                    Records = [CreateSelection("0123456789", "MALL DEL SOL")]
                },
                CancellationToken.None);

            var result = Assert.Single(summary.Results);
            Assert.True(result.Success, result.ErrorMessage);
            using var workbook = new XLWorkbook(result.OutputPath!);
            Assert.Single(workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3").Pictures);
            Assert.Single(workbook.Worksheet("CERTIFICADO").Pictures);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_UsesMissingCostCenterFolderAndUniqueName()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var request = new MedicalGenerationRequest
            {
                TemplatePath = TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Retiro),
                OutputBaseDirectory = outputDirectory.FullName,
                EvaluationType = OccupationalEvaluationType.Retiro,
                GeneratedAt = new DateTime(2026, 7, 10),
                AttentionDate = new DateTime(2026, 7, 10),
                Company = CreateCompany(),
                Records = [CreateSelection("0001"), CreateSelection("0001")]
            };

            var summary = await service.GenerateAsync(request, CancellationToken.None);

            Assert.Equal(2, summary.SucceededCount);
            Assert.All(summary.Results, result => Assert.Contains("SIN CENTRO DE COSTO", result.OutputPath!, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(summary.Results, result => result.OutputPath!.Contains("(2).xlsx", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task GenerateAsync_WritesFemaleMarkOnlyOnFirstSheet()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var templatePath = TestPaths.MedicalTemplatePath(OccupationalEvaluationType.Periodica);
            using var templateWorkbook = new XLWorkbook(templatePath);
            var originalCertificateFormula = templateWorkbook.Worksheet("CERTIFICADO").Cell("AD6").FormulaA1;

            var selection = CreateSelection("9999", "MALL DEL SOL", EmployeeGender.Femenino);
            var summary = await service.GenerateAsync(
                new MedicalGenerationRequest
                {
                    TemplatePath = templatePath,
                    OutputBaseDirectory = outputDirectory.FullName,
                    EvaluationType = OccupationalEvaluationType.Periodica,
                    GeneratedAt = new DateTime(2026, 7, 10),
                    AttentionDate = new DateTime(2026, 7, 10),
                    Company = CreateCompany(),
                    Records = [selection]
                },
                CancellationToken.None);

            using var workbook = new XLWorkbook(summary.Results[0].OutputPath!);
            var evaluationSheet = workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3");
            var certificateSheet = workbook.Worksheet("CERTIFICADO");

            Assert.Equal(string.Empty, evaluationSheet.Cell("U13").GetString());
            Assert.Equal("X", evaluationSheet.Cell("X13").GetString());
            Assert.Equal(originalCertificateFormula, certificateSheet.Cell("AD6").FormulaA1);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    private static MedicalEvaluationGenerationService CreateGenerationService()
    {
        var safeFileNameService = new SafeFileNameService();
        var outputOrganizationService = new OutputOrganizationService(safeFileNameService);
        var templateWriter = new MedicalTemplateWriter(new AgeCalculationService());
        return new MedicalEvaluationGenerationService(outputOrganizationService, templateWriter);
    }

    private static CompanyProfile CreateCompany()
    {
        return new CompanyProfile
        {
            Institution = "MANLIM S.A.S",
            Ruc = "'0992424362001",
            Ciiu = "N8110.00"
        };
    }

    private static EmployeeGenerationSelection CreateSelection(
        string identification,
        string? costCenter = null,
        EmployeeGender gender = EmployeeGender.Masculino)
    {
        return new EmployeeGenerationSelection
        {
            RowNumber = 4,
            Gender = gender,
            Record = new EmployeeRecord
            {
                Identificacion = identification,
                Apellidos = "MOLINA TORRES",
                Nombres = "ROSALINDA ELIZABETH",
                FechaNacimiento = new DateTime(2002, 11, 5),
                FechaIngreso = new DateTime(2024, 1, 15),
                FechaLiquidacion = new DateTime(2026, 7, 1),
                Movil = "0999999999",
                CentroCosto = costCenter,
                Oficio = "AUXILIAR DE LIMPIEZA (9112)"
            },
            NameParts = new EmployeeNameParts
            {
                PrimerApellido = "MOLINA",
                SegundoApellido = "TORRES",
                PrimerNombre = "ROSALINDA",
                SegundoNombre = "ELIZABETH"
            }
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void AssertFormulaCellsUnchanged(IXLWorksheet expected, IXLWorksheet actual)
    {
        foreach (var expectedCell in expected.CellsUsed().Where(static cell => cell.HasFormula))
        {
            Assert.Equal(expectedCell.FormulaA1, actual.Cell(expectedCell.Address).FormulaA1);
        }
    }

    private static void AssertPictureIsAnchoredInside(
        IXLPicture picture,
        int firstRow,
        int firstColumn,
        int lastRow,
        int lastColumn)
    {
        Assert.InRange(picture.TopLeftCell.Address.RowNumber, firstRow, lastRow);
        Assert.InRange(picture.TopLeftCell.Address.ColumnNumber, firstColumn, lastColumn);
        Assert.InRange(picture.BottomRightCell.Address.RowNumber, firstRow, lastRow);
        Assert.InRange(picture.BottomRightCell.Address.ColumnNumber, firstColumn, lastColumn);
    }

    private static void AssertSheetValuesAndFormulasEqual(IXLWorksheet expected, IXLWorksheet actual)
    {
        Assert.Equal(expected.MergedRanges.Count(), actual.MergedRanges.Count());
        foreach (var expectedCell in expected.CellsUsed())
        {
            var actualCell = actual.Cell(expectedCell.Address);
            Assert.Equal(expectedCell.FormulaA1, actualCell.FormulaA1);
            Assert.Equal(expectedCell.GetString(), actualCell.GetString());
        }
    }
}
