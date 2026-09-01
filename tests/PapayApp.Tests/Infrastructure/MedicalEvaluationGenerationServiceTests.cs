using System.Security.Cryptography;
using ClosedXML.Excel;
using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class MedicalEvaluationGenerationServiceTests
{
    [Theory]
    [InlineData(OccupationalEvaluationType.Periodica, "Z19", "U11", null, null, "EV PERIODICA - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Preocupacional, "G19", "L11", "P28", "I16", "EV INICIAL - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Reintegro, "AI19", "AC11", null, null, "EV REINTEGRO - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    [InlineData(OccupationalEvaluationType.Retiro, "BA19", "AI11", "BB28", "AK16", "EV RETIRO - ROSALINDA ELIZABETH MOLINA TORRES.xlsx")]
    public async Task GenerateAsync_CreatesWorkbookWithExpectedValues(
        OccupationalEvaluationType evaluationType,
        string evaluationMarkCell,
        string certificateMarkCell,
        string? aptitudeMarkCell,
        string? certificateAptitudeMarkCell,
        string expectedFileName)
    {
        var templatePath = TestPaths.TemplatePath;
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
                    Records =
                    [
                        new EmployeeGenerationSelection
                        {
                            RowNumber = 4,
                            Gender = EmployeeGender.Masculino,
                            Record = new EmployeeRecord
                            {
                                Identificacion = "0123456789",
                                Apellidos = "MOLINA TORRES",
                                Nombres = "ROSALINDA ELIZABETH",
                                FechaNacimiento = new DateTime(2002, 11, 5),
                                FechaIngreso = new DateTime(2024, 1, 15),
                                FechaLiquidacion = new DateTime(2026, 7, 1),
                                Movil = "0999999999",
                                CentroCosto = "RIOCENTRO QUITO"
                            },
                            NameParts = new EmployeeNameParts
                            {
                                PrimerApellido = "MOLINA",
                                SegundoApellido = "TORRES",
                                PrimerNombre = "ROSALINDA",
                                SegundoNombre = "ELIZABETH"
                            }
                        }
                    ]
                },
                CancellationToken.None);

            Assert.Single(summary.Results);
            var result = summary.Results[0];
            Assert.True(result.Success);
            Assert.NotNull(result.OutputPath);
            Assert.EndsWith(expectedFileName, result.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.OutputPath));

            using var workbook = new XLWorkbook(result.OutputPath);
            var evaluationSheet = workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3");
            var occupationalSheet = workbook.Worksheet("EVALUACION OCUPACIONAL2-3");
            var thirdSheet = workbook.Worksheet("EVALUACION OCUPACIONAL 3-3");
            var certificateSheet = workbook.Worksheet("CERTIFICADO");

            Assert.Equal("A. DATOS DEL ESTABLECIMIENTO - DATOS DEL USUARIO                                                                                               TELEF: 0999999999", evaluationSheet.Cell("B3").GetString());
            Assert.Equal("0123456789", evaluationSheet.Cell("AP5").GetString());
            Assert.Equal("MOLINA", evaluationSheet.Cell("B8").GetString());
            Assert.Equal("TORRES", evaluationSheet.Cell("S8").GetString());
            Assert.Equal("ROSALINDA", evaluationSheet.Cell("AH8").GetString());
            Assert.Equal("ELIZABETH", evaluationSheet.Cell("AR8").GetString());
            Assert.Equal("X", evaluationSheet.Cell("U13").GetString());
            Assert.Equal("2002", evaluationSheet.Cell("Z13").GetFormattedString());
            Assert.Equal("11", evaluationSheet.Cell("AC13").GetFormattedString());
            Assert.Equal("5", evaluationSheet.Cell("AE13").GetFormattedString());
            Assert.Equal("23", evaluationSheet.Cell("AF13").GetFormattedString());
            Assert.Equal("AUXILIAR DE LIMPIEZA / RIOCENTRO QUITO", evaluationSheet.Cell("J16").GetString());
            Assert.Equal(string.Empty, evaluationSheet.Cell("AH16").GetString());
            Assert.Equal("X", evaluationSheet.Cell(evaluationMarkCell).GetString());
            Assert.Equal(string.Empty, evaluationSheet.Cell("X13").GetString());

            Assert.Equal("AUXILIAR DE LIMPIEZA / RIOCENTRO QUITO", occupationalSheet.Cell("G2").GetString());

            AssertAptitudeMarks(thirdSheet, ["P28", "AE28", "AS28", "BB28"], aptitudeMarkCell);

            Assert.Equal("A. DATOS DEL ESTABLECIMIENTO - DATOS DEL USUARIO                                                                                               TELEF: 0999999999", certificateSheet.Cell("A2").GetString());
            Assert.Equal("0123456789", certificateSheet.Cell("AC4").GetString());
            Assert.Equal("MOLINA", certificateSheet.Cell("A6").GetString());
            Assert.Equal("TORRES", certificateSheet.Cell("J6").GetString());
            Assert.Equal("ROSALINDA", certificateSheet.Cell("Q6").GetString());
            Assert.Equal("ELIZABETH", certificateSheet.Cell("X6").GetString());
            Assert.Equal("masculino", certificateSheet.Cell("AD6").GetString());
            Assert.Equal("AUXILIAR DE LIMPIEZA / RIOCENTRO QUITO", certificateSheet.Cell("AG6").GetString());
            Assert.Equal("0", certificateSheet.Cell("M9").GetString());
            Assert.Equal("7", certificateSheet.Cell("N9").GetString());
            Assert.Equal("1", certificateSheet.Cell("O9").GetString());
            Assert.Equal("0", certificateSheet.Cell("P9").GetString());
            Assert.Equal("X", certificateSheet.Cell(certificateMarkCell).GetString());
            AssertAptitudeMarks(certificateSheet, ["I16", "S16", "AC16", "AK16"], certificateAptitudeMarkCell);

            using var templateWorkbook = new XLWorkbook(templatePath);
            var originalThirdSheet = templateWorkbook.Worksheet("EVALUACION OCUPACIONAL 3-3");
            Assert.Equal(originalThirdSheet.MergedRanges.Count(), thirdSheet.MergedRanges.Count());
            Assert.Equal(originalThirdSheet.Cell("A1").GetString(), thirdSheet.Cell("A1").GetString());
            Assert.Equal(originalThirdSheet.Cell("BM41").GetString(), thirdSheet.Cell("BM41").GetString());

            var newTemplateHash = ComputeSha256(templatePath);
            Assert.Equal(originalTemplateHash, newTemplateHash);
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
                TemplatePath = TestPaths.TemplatePath,
                OutputBaseDirectory = outputDirectory.FullName,
                EvaluationType = OccupationalEvaluationType.Retiro,
                GeneratedAt = new DateTime(2026, 7, 10),
                Records =
                [
                    CreateSelection("0001"),
                    CreateSelection("0001")
                ]
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
    public async Task GenerateAsync_WritesFemaleMarksWhenSelected()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var service = CreateGenerationService();
            var request = new MedicalGenerationRequest
            {
                TemplatePath = TestPaths.TemplatePath,
                OutputBaseDirectory = outputDirectory.FullName,
                EvaluationType = OccupationalEvaluationType.Periodica,
                GeneratedAt = new DateTime(2026, 7, 10),
                Records =
                [
                    new EmployeeGenerationSelection
                    {
                        RowNumber = 5,
                        Gender = EmployeeGender.Femenino,
                        Record = new EmployeeRecord
                        {
                            Identificacion = "9999",
                            Apellidos = "MORALES",
                            Nombres = "ANA",
                            CentroCosto = "MALL DEL SOL"
                        },
                        NameParts = new EmployeeNameParts
                        {
                            PrimerApellido = "MORALES",
                            PrimerNombre = "ANA"
                        }
                    }
                ]
            };

            var summary = await service.GenerateAsync(request, CancellationToken.None);
            using var workbook = new XLWorkbook(summary.Results[0].OutputPath!);
            var evaluationSheet = workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3");
            var certificateSheet = workbook.Worksheet("CERTIFICADO");

            Assert.Equal(string.Empty, evaluationSheet.Cell("U13").GetString());
            Assert.Equal("X", evaluationSheet.Cell("X13").GetString());
            Assert.Equal("femenino", certificateSheet.Cell("AD6").GetString());
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

    private static EmployeeGenerationSelection CreateSelection(string identification)
    {
        return new EmployeeGenerationSelection
        {
            RowNumber = 4,
            Gender = EmployeeGender.Masculino,
            Record = new EmployeeRecord
            {
                Identificacion = identification,
                Apellidos = "MOLINA TORRES",
                Nombres = "ROSALINDA ELIZABETH",
                Movil = null,
                CentroCosto = null
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
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static void AssertAptitudeMarks(IXLWorksheet worksheet, string[] markCells, string? expectedMarkCell)
    {
        foreach (var address in markCells)
        {
            var cell = worksheet.Cell(address);
            if (address == expectedMarkCell)
            {
                Assert.Equal("X", cell.GetString());
                Assert.Equal(XLColor.Red, cell.Style.Font.FontColor);
            }
            else
            {
                Assert.Equal(string.Empty, cell.GetString());
            }
        }
    }
}
