using ClosedXML.Excel;
using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class ExcelImportServiceTests
{
    [Fact]
    public async Task ImportWorksheetAsync_DetectsHeadersAndPreservesTextFields()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("INGRESOS");
                sheet.Cell("A3").Value = "Cédula";
                sheet.Cell("B3").Value = "Apellidos";
                sheet.Cell("C3").Value = "Nombres";
                sheet.Cell("D3").Value = "Teléfono";
                sheet.Cell("E3").Value = "Fecha Nacimiento";
                sheet.Cell("F3").Value = "Fecha Ingreso";
                sheet.Cell("G3").Value = "Centro Costo";

                sheet.Cell("A4").SetValue("0123456789");
                sheet.Cell("B4").SetValue("MOLINA TORRES");
                sheet.Cell("C4").SetValue("ROSALINDA ELIZABETH");
                sheet.Cell("D4").SetValue("0987654321");
                sheet.Cell("E4").Value = 44870;
                sheet.Cell("F4").Value = "2024-05-01";
                sheet.Cell("G4").SetValue("RIOCENTRO QUITO");

                workbook.SaveAs(tempFile);
            }

            var service = new ExcelImportService(new HeaderNormalizer(), new NameSplitService());

            var preview = await service.ImportWorksheetAsync(tempFile, "INGRESOS", CancellationToken.None);

            Assert.Equal(3, preview.HeaderRowNumber);
            Assert.Contains(preview.Headers, static header => header.Field == EmployeeField.Identificacion);
            Assert.Single(preview.Records);

            var row = preview.Records[0];
            Assert.Equal("0123456789", row.Record.Identificacion);
            Assert.Equal("0987654321", row.Record.Movil);
            Assert.Equal("MOLINA", row.NameParts.PrimerApellido);
            Assert.Equal("TORRES", row.NameParts.SegundoApellido);
            Assert.Equal(ImportRecordStatus.Valid, row.Status);
            Assert.NotNull(row.Record.FechaNacimiento);
            Assert.NotNull(row.Record.FechaIngreso);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ImportWorksheetAsync_ReportsMissingIdentificationAsOmitted()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("RETIRO");
                sheet.Cell("A1").Value = "Apellidos";
                sheet.Cell("B1").Value = "Nombres";
                sheet.Cell("A2").Value = "TOCTAQUIZA";
                sheet.Cell("B2").Value = "JAIRO";
                workbook.SaveAs(tempFile);
            }

            var service = new ExcelImportService(new HeaderNormalizer(), new NameSplitService());
            var preview = await service.ImportWorksheetAsync(tempFile, "RETIRO", CancellationToken.None);

            Assert.Single(preview.Records);
            Assert.Equal(ImportRecordStatus.Omitted, preview.Records[0].Status);
            Assert.Contains(preview.Records[0].Warnings, warning => warning.Contains("identificación", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ImportWorksheetAsync_IgnoresUnusedColumnsAndResolvesCommonHeaderVariants()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("RETIROS");
                sheet.Cell("A2").Value = "Correo Electronico";
                sheet.Cell("B2").Value = "Cédula";
                sheet.Cell("C2").Value = "Apellidos";
                sheet.Cell("D2").Value = "Nombres";
                sheet.Cell("E2").Value = "Fecha de Retiro";
                sheet.Cell("F2").Value = "  ";
                sheet.Cell("G2").Value = "Celular 1";

                sheet.Cell("A3").Value = "persona@correo.com";
                sheet.Cell("B3").SetValue("0102030405");
                sheet.Cell("C3").SetValue("PEREZ LOPEZ");
                sheet.Cell("D3").SetValue("ANA MARIA");
                sheet.Cell("E3").Value = "10/07/2026";
                sheet.Cell("G3").SetValue("0991112233");

                workbook.SaveAs(tempFile);
            }

            var service = new ExcelImportService(new HeaderNormalizer(), new NameSplitService());
            var preview = await service.ImportWorksheetAsync(tempFile, "RETIROS", CancellationToken.None);

            Assert.Single(preview.Records);
            Assert.Contains(preview.Headers, static header => header.Field == EmployeeField.Identificacion);
            Assert.Contains(preview.Headers, static header => header.Field == EmployeeField.FechaLiquidacion);
            Assert.Contains(preview.UnrecognizedHeaders, static header => header.Contains("Correo", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("0102030405", preview.Records[0].Record.Identificacion);
            Assert.Equal("0991112233", preview.Records[0].Record.Movil);
            Assert.Equal(new DateTime(2026, 7, 10), preview.Records[0].Record.FechaLiquidacion);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetWorksheetNamesAsync_ReturnsOnlyIngresoAndRetiroRelatedSheets()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                workbook.AddWorksheet("Nomina");
                workbook.AddWorksheet("Ingresos");
                workbook.AddWorksheet("Salidas");
                workbook.AddWorksheet("Resumen");
                workbook.SaveAs(tempFile);
            }

            var service = new ExcelImportService(new HeaderNormalizer(), new NameSplitService());
            var worksheetNames = await service.GetWorksheetNamesAsync(tempFile, CancellationToken.None);

            Assert.Equal(2, worksheetNames.Count);
            Assert.Contains("Ingresos", worksheetNames);
            Assert.Contains("Salidas", worksheetNames);
            Assert.DoesNotContain("Nomina", worksheetNames);
            Assert.DoesNotContain("Resumen", worksheetNames);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
