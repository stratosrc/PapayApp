using ClosedXML.Excel;
using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class CertificateImportServiceTests
{
    [Fact]
    public async Task ImportAsync_ReadsFixedColumnsAndPreservesLeadingZeroes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Sheet1");
                sheet.Cell("A1").Value = "Apellidos y Nombres:";
                sheet.Cell("B1").Value = "Número de cédula:";
                sheet.Cell("A2").Value = "MELANIE PARRA PIBAQUE";
                sheet.Cell("B2").SetValue("0953456423");
                sheet.Cell("A3").Value = "otra persona";
                sheet.Cell("B3").SetValue("0953456423");
                workbook.SaveAs(path);
            }

            var service = new CertificateImportService();
            var result = await service.ImportAsync(path);

            Assert.True(result.IsValid);
            Assert.Equal(2, result.Participants.Count);
            Assert.Equal("Melanie Parra Pibaque", result.Participants[0].FullName);
            Assert.Equal("Otra Persona", result.Participants[1].FullName);
            Assert.Equal("0953456423", result.Participants[0].Identification);
            Assert.Contains(result.Warnings, warning => warning.Contains("repetida", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsIncorrectColumnStructure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Sheet1");
                sheet.Cell("A1").Value = "Cédula";
                sheet.Cell("B1").Value = "Nombres";
                sheet.Cell("A2").Value = "0953456423";
                sheet.Cell("B2").Value = "Melanie Parra";
                workbook.SaveAs(path);
            }

            var service = new CertificateImportService();
            var result = await service.ImportAsync(path);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("columna A", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Errors, error => error.Contains("columna B", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
