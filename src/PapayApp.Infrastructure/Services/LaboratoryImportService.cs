using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class LaboratoryImportService : ILaboratoryImportService
{
    private static readonly string[] Required = ["NOMBRES Y APELLIDOS", "CEDULA", "EDAD", "GENERO"];
    public static string Normalize(string text) => Regex.Replace(
        new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();

    private static (int Row, Dictionary<string, int> Columns)? Headers(IXLWorksheet sheet)
    {
        foreach (var row in sheet.Rows(1, Math.Min(30, sheet.LastRowUsed()?.RowNumber() ?? 1)))
        {
            var headers = row.CellsUsed().Select(c => (Name: Normalize(c.GetFormattedString()), Col: c.Address.ColumnNumber))
                .GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.First().Col);
            if (Required.All(headers.ContainsKey)) return (row.RowNumber(), headers);
        }
        return null;
    }

    public Task<IReadOnlyList<string>> GetWorksheetNamesAsync(string path, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<string>>(() =>
    {
        using var book = new XLWorkbook(path);
        return book.Worksheets.Where(sheet => { cancellationToken.ThrowIfCancellationRequested(); return Headers(sheet) is not null; }).Select(s => s.Name).ToList();
    }, cancellationToken);

    public Task<LaboratoryImportResult> ImportAsync(string path, string worksheet, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        using var book = new XLWorkbook(path);
        var sheet = book.Worksheet(worksheet);
        var header = Headers(sheet) ?? throw new InvalidDataException("La hoja necesita NOMBRES Y APELLIDOS, CEDULA, EDAD y GENERO en una misma fila entre las primeras 30 filas.");
        var patients = new List<LaboratoryPatient>();
        var issues = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var number = header.Row + 1; number <= (sheet.LastRowUsed()?.RowNumber() ?? header.Row); number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = sheet.Row(number);
            string Read(string name) => header.Columns.TryGetValue(name, out var col) ? row.Cell(col).GetFormattedString().Trim() : "";
            var name = Read("NOMBRES Y APELLIDOS");
            var idCell = row.Cell(header.Columns["CEDULA"]);
            var id = Read("CEDULA").Replace(" ", "", StringComparison.Ordinal);
            // Numeric Ecuadorian IDs may lose the leading zero in Excel.
            if (idCell.DataType == XLDataType.Number && idCell.TryGetValue<double>(out var n) && n >= 0 && n == Math.Truncate(n)) id = n.ToString("0000000000", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id)) continue;
            if (Normalize(name) is "TOTAL" or "TOTALES") continue;
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(name)) missing.Add("nombre y apellidos");
            if (id.Length != 10 || id.Any(c => !char.IsAsciiDigit(c))) missing.Add("cédula de 10 dígitos");
            if (!int.TryParse(Read("EDAD"), out var age) || age < 0 || age > 120) missing.Add("edad entre 0 y 120");
            var gender = Normalize(Read("GENERO"));
            if (gender is not ("M" or "F")) missing.Add("género M o F");
            if (missing.Count > 0) { issues.Add($"Fila {number}: corrige {string.Join(", ", missing)}. Registro excluido."); continue; }
            if (!ids.Add(id)) { issues.Add($"Fila {number}: cédula repetida; se conserva solo la primera fila de esa persona."); continue; }
            patients.Add(new(number, name, id, age, gender, Read("TEST")));
        }
        if (patients.Count == 0) issues.Add("No hay pacientes válidos para generar. Revisa las columnas obligatorias.");
        return new LaboratoryImportResult { Patients = patients, Issues = issues };
    }, cancellationToken);
}
