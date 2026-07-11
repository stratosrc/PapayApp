using ClosedXML.Excel;
using System.Globalization;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class ExcelImportService : IExcelImportService
{
    private static readonly string[] IngresoWorksheetKeywords = { "INGRESO", "INGRESOS", "ALTA", "ALTAS" };
    private static readonly string[] RetiroWorksheetKeywords = { "RETIRO", "RETIROS", "SALIDA", "SALIDAS", "BAJA", "BAJAS", "LIQUIDACION", "LIQUIDACIONES" };

    private static readonly CultureInfo[] SupportedDateCultures =
    {
        CultureInfo.GetCultureInfo("es-MX"),
        CultureInfo.InvariantCulture
    };

    private static readonly string[] SupportedDateFormats =
    {
        "dd/MM/yyyy",
        "d/M/yyyy",
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy"
    };

    private static readonly EmployeeField[] RequiredHeaderCandidates =
    {
        EmployeeField.Identificacion,
        EmployeeField.Apellidos,
        EmployeeField.Nombres
    };

    private readonly HeaderNormalizer _headerNormalizer;
    private readonly INameSplitService _nameSplitService;

    public ExcelImportService(HeaderNormalizer headerNormalizer, INameSplitService nameSplitService)
    {
        _headerNormalizer = headerNormalizer;
        _nameSplitService = nameSplitService;
    }

    public Task<IReadOnlyList<string>> GetWorksheetNamesAsync(string filePath, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInputFileExists(filePath);

            using var workbook = new XLWorkbook(filePath);
            return workbook.Worksheets
                .Select(static sheet => sheet.Name)
                .Where(IsRelevantWorksheetName)
                .ToList();
        }, cancellationToken);
    }

    public Task<WorksheetImportPreview> ImportWorksheetAsync(string filePath, string worksheetName, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInputFileExists(filePath);

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(worksheetName)
                ?? throw new InvalidOperationException($"La hoja '{worksheetName}' no existe en el archivo maestro.");

            var headerRowNumber = DetectHeaderRow(worksheet);
            var headerCells = worksheet.Row(headerRowNumber).CellsUsed().ToList();
            if (headerCells.Count == 0)
            {
                throw new InvalidOperationException("No se encontraron encabezados en la hoja seleccionada.");
            }

            var recognizedCounts = new Dictionary<EmployeeField, int>();
            var headerMatches = new List<HeaderMatch>();
            var columnMap = new Dictionary<EmployeeField, int>();
            var duplicateHeaders = new List<string>();

            foreach (var cell in headerCells)
            {
                var originalHeader = cell.GetString();
                var field = _headerNormalizer.ResolveField(originalHeader);
                var isDuplicate = false;

                if (field.HasValue)
                {
                    recognizedCounts.TryGetValue(field.Value, out var count);
                    recognizedCounts[field.Value] = count + 1;
                    isDuplicate = count > 0;

                    if (!isDuplicate)
                    {
                        columnMap[field.Value] = cell.Address.ColumnNumber;
                    }
                    else
                    {
                        duplicateHeaders.Add(originalHeader);
                    }
                }

                headerMatches.Add(new HeaderMatch
                {
                    OriginalHeader = originalHeader,
                    NormalizedHeader = _headerNormalizer.Normalize(originalHeader),
                    Field = field,
                    IsDuplicate = isDuplicate
                });
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
            var records = new List<ImportedEmployeeRecord>();

            for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = worksheet.Row(rowNumber);
                if (row.IsEmpty())
                {
                    continue;
                }

                var warnings = new List<string>();
                var record = new EmployeeRecord
                {
                    Identificacion = ReadText(row, columnMap, EmployeeField.Identificacion),
                    Apellidos = ReadText(row, columnMap, EmployeeField.Apellidos),
                    Nombres = ReadText(row, columnMap, EmployeeField.Nombres),
                    Movil = ReadText(row, columnMap, EmployeeField.Movil),
                    CentroCosto = ReadText(row, columnMap, EmployeeField.CentroCosto),
                    FechaNacimiento = ReadDate(row, columnMap, EmployeeField.FechaNacimiento, warnings),
                    FechaIngreso = ReadDate(row, columnMap, EmployeeField.FechaIngreso, warnings),
                    FechaLiquidacion = ReadDate(row, columnMap, EmployeeField.FechaLiquidacion, warnings),
                    Edad = ReadInt(row, columnMap, EmployeeField.Edad, warnings)
                };

                if (IsCompletelyEmpty(record))
                {
                    continue;
                }

                var status = ImportRecordStatus.Valid;

                if (string.IsNullOrWhiteSpace(record.Identificacion))
                {
                    warnings.Add("Registro omitido: falta la identificación.");
                    status = ImportRecordStatus.Omitted;
                }

                if (string.IsNullOrWhiteSpace(record.Apellidos) || string.IsNullOrWhiteSpace(record.Nombres))
                {
                    warnings.Add("Nombre incompleto: revisa nombres y apellidos antes de generar.");
                    if (status == ImportRecordStatus.Valid)
                    {
                        status = ImportRecordStatus.Incomplete;
                    }
                }

                if (!string.IsNullOrWhiteSpace(record.Movil) && record.Movil.Trim().Length < 7)
                {
                    warnings.Add("Teléfono sospechosamente corto.");
                }

                if (string.IsNullOrWhiteSpace(record.CentroCosto))
                {
                    warnings.Add("Centro de costo vacío: se usará la carpeta SIN CENTRO DE COSTO.");
                }

                var nameParts = _nameSplitService.Split(record.Nombres, record.Apellidos);
                records.Add(new ImportedEmployeeRecord
                {
                    RowNumber = rowNumber,
                    Record = record,
                    NameParts = nameParts,
                    Status = status,
                    Warnings = warnings
                });
            }

            return new WorksheetImportPreview
            {
                WorksheetName = worksheetName,
                HeaderRowNumber = headerRowNumber,
                Headers = headerMatches,
                Records = records,
                RecognizedHeaders = headerMatches
                    .Where(static match => match.IsRecognized)
                    .Select(static match => $"{match.OriginalHeader} -> {match.CanonicalName}")
                    .ToList(),
                UnrecognizedHeaders = headerMatches
                    .Where(static match => !match.IsRecognized)
                    .Select(static match => match.OriginalHeader)
                    .ToList(),
                DuplicateHeaders = duplicateHeaders,
                ValidCount = records.Count(static record => record.Status == ImportRecordStatus.Valid),
                IncompleteCount = records.Count(static record => record.Status == ImportRecordStatus.Incomplete),
                OmittedCount = records.Count(static record => record.Status == ImportRecordStatus.Omitted)
            };
        }, cancellationToken);
    }

    private static void EnsureInputFileExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("No se encontró el archivo maestro.", filePath);
        }
    }

    private int DetectHeaderRow(IXLWorksheet worksheet)
    {
        var lastRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 20, 20);
        var bestRowNumber = 1;
        var bestScore = -1;

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var cells = worksheet.Row(rowNumber).CellsUsed().ToList();
            if (cells.Count == 0)
            {
                continue;
            }

            var recognized = 0;
            foreach (var cell in cells)
            {
                if (_headerNormalizer.ResolveField(cell.GetString()).HasValue)
                {
                    recognized++;
                }
            }

            if (recognized > bestScore)
            {
                bestScore = recognized;
                bestRowNumber = rowNumber;
            }
        }

        if (bestScore < 2)
        {
            throw new InvalidOperationException("No fue posible detectar la fila de encabezados.");
        }

        return bestRowNumber;
    }

    private bool IsRelevantWorksheetName(string worksheetName)
    {
        var normalized = _headerNormalizer.Normalize(worksheetName);
        return IngresoWorksheetKeywords.Any(normalized.Contains)
            || RetiroWorksheetKeywords.Any(normalized.Contains);
    }

    private static string? ReadText(IXLRow row, IReadOnlyDictionary<EmployeeField, int> columnMap, EmployeeField field)
    {
        if (!columnMap.TryGetValue(field, out var columnNumber))
        {
            return null;
        }

        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        var text = cell.GetFormattedString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? ReadInt(
        IXLRow row,
        IReadOnlyDictionary<EmployeeField, int> columnMap,
        EmployeeField field,
        ICollection<string> warnings)
    {
        if (!columnMap.TryGetValue(field, out var columnNumber))
        {
            return null;
        }

        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (int.TryParse(cell.GetFormattedString().Trim(), out intValue))
        {
            return intValue;
        }

        warnings.Add($"No se pudo convertir el campo {field} en la fila {row.RowNumber()}.");
        return null;
    }

    private static DateTime? ReadDate(
        IXLRow row,
        IReadOnlyDictionary<EmployeeField, int> columnMap,
        EmployeeField field,
        ICollection<string> warnings)
    {
        if (!columnMap.TryGetValue(field, out var columnNumber))
        {
            return null;
        }

        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var date))
        {
            return date.Date;
        }

        if (cell.DataType == XLDataType.Number &&
            double.TryParse(cell.GetValue<string>(), out var numericDate))
        {
            try
            {
                return DateTime.FromOADate(numericDate).Date;
            }
            catch (ArgumentException)
            {
                warnings.Add($"No se pudo convertir la fecha del campo {field} en la fila {row.RowNumber()}.");
                return null;
            }
        }

        var text = cell.GetFormattedString().Trim();
        foreach (var culture in SupportedDateCultures)
        {
            if (DateTime.TryParseExact(
                text,
                SupportedDateFormats,
                culture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
            {
                return date.Date;
            }
        }

        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return date.Date;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
        {
            return date.Date;
        }

        warnings.Add($"No se pudo convertir la fecha del campo {field} en la fila {row.RowNumber()}.");
        return null;
    }

    private static bool IsCompletelyEmpty(EmployeeRecord record)
    {
        return string.IsNullOrWhiteSpace(record.Identificacion)
            && string.IsNullOrWhiteSpace(record.Apellidos)
            && string.IsNullOrWhiteSpace(record.Nombres)
            && string.IsNullOrWhiteSpace(record.Movil)
            && string.IsNullOrWhiteSpace(record.CentroCosto)
            && record.FechaNacimiento is null
            && record.FechaIngreso is null
            && record.FechaLiquidacion is null
            && record.Edad is null;
    }
}
