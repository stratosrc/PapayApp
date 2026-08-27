using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class CertificateImportService : ICertificateImportService
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumericRegex = new(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);

    private static readonly HashSet<string> ValidNameHeaders = new(StringComparer.Ordinal)
    {
        "NOMBRE",
        "NOMBRES",
        "NOMBRE Y APELLIDOS",
        "APELLIDOS Y NOMBRES"
    };

    private static readonly HashSet<string> ValidIdentificationHeaders = new(StringComparer.Ordinal)
    {
        "CEDULA",
        "NUMERO DE CEDULA",
        "CEDULA DE IDENTIFICACION",
        "IDENTIFICACION"
    };

    public Task<CertificateImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Import(filePath, cancellationToken), cancellationToken);
    }

    private static CertificateImportResult Import(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return Invalid("El archivo Excel no existe o no se pudo encontrar.");
        }

        if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("El archivo debe tener formato .xlsx.");
        }

        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                return Invalid("El archivo no contiene ninguna hoja de cálculo.");
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            ValidateHeaders(worksheet, errors);

            var participants = new List<CertificateParticipant>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = worksheet.Row(rowNumber);
                var fullName = PersonNameFormatter.ToTitleCase(ReadText(row.Cell(1)));
                var identification = ReadText(row.Cell(2));

                if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(identification))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    errors.Add($"Fila {rowNumber}: falta el nombre en la columna A.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(identification))
                {
                    errors.Add($"Fila {rowNumber}: falta la cédula en la columna B.");
                    continue;
                }

                if (identification.Any(static character => !char.IsDigit(character)))
                {
                    errors.Add($"Fila {rowNumber}: la cédula debe contener únicamente números.");
                    continue;
                }

                if (identification.Length != 10)
                {
                    errors.Add($"Fila {rowNumber}: la cédula debe contener exactamente 10 dígitos.");
                    continue;
                }

                participants.Add(new CertificateParticipant(rowNumber, fullName, identification));
            }

            if (participants.Count == 0 && errors.Count == 0)
            {
                errors.Add("No se encontraron personas desde la fila 2.");
            }

            AddDuplicateWarnings(participants, warnings);

            return new CertificateImportResult
            {
                WorksheetName = worksheet.Name,
                Participants = participants,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Invalid($"No fue posible leer el Excel: {exception.Message}");
        }
    }

    private static void ValidateHeaders(IXLWorksheet worksheet, ICollection<string> errors)
    {
        var nameHeader = NormalizeHeader(ReadText(worksheet.Cell(1, 1)));
        var identificationHeader = NormalizeHeader(ReadText(worksheet.Cell(1, 2)));

        if (!ValidNameHeaders.Contains(nameHeader))
        {
            errors.Add(
                "La columna A debe contener el encabezado 'Apellidos y Nombres' o 'Nombre y Apellidos'.");
        }

        if (!ValidIdentificationHeaders.Contains(identificationHeader))
        {
            errors.Add(
                "La columna B debe contener el encabezado 'Número de cédula' o 'Cédula'.");
        }
    }

    private static void AddDuplicateWarnings(
        IReadOnlyCollection<CertificateParticipant> participants,
        ICollection<string> warnings)
    {
        var duplicateIdentifications = participants
            .GroupBy(static participant => participant.Identification, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToList();

        if (duplicateIdentifications.Count > 0)
        {
            warnings.Add(
                $"Hay {duplicateIdentifications.Count} cédula(s) repetida(s). Se generará un certificado por cada fila.");
        }

        var duplicateNames = participants
            .GroupBy(static participant => participant.FullName, StringComparer.OrdinalIgnoreCase)
            .Count(static group => group.Count() > 1);

        if (duplicateNames > 0)
        {
            warnings.Add(
                $"Hay {duplicateNames} nombre(s) repetido(s). Los folios evitarán nombres de archivo duplicados.");
        }
    }

    private static string ReadText(IXLCell cell)
    {
        return cell.IsEmpty() ? string.Empty : cell.GetFormattedString().Trim();
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var withoutDiacritics = builder.ToString().Normalize(NormalizationForm.FormC);
        var withoutPunctuation = NonAlphaNumericRegex.Replace(withoutDiacritics, " ");
        return MultiWhitespaceRegex.Replace(withoutPunctuation, " ").Trim().ToUpperInvariant();
    }

    private static CertificateImportResult Invalid(string error)
    {
        return new CertificateImportResult { Errors = new[] { error } };
    }
}
