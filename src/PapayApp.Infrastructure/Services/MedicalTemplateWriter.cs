using System.Drawing;
using ClosedXML.Excel;
using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class MedicalTemplateWriter
{
    private const string PhoneHeaderPrefix = "A. DATOS DEL ESTABLECIMIENTO - DATOS DEL USUARIO                                                                                               TELEF: ";

    private readonly IAgeCalculationService _ageCalculationService;

    public MedicalTemplateWriter(IAgeCalculationService ageCalculationService)
    {
        _ageCalculationService = ageCalculationService;
    }

    public void Write(
        string templatePath,
        string outputPath,
        EmployeeGenerationSelection selection,
        OccupationalEvaluationType evaluationType,
        DateTime generatedAt,
        DateTime? attentionDate,
        CompanyProfile company,
        string? logoPath)
    {
        using var workbook = new XLWorkbook(templatePath);
        var record = selection.Record;
        var nameParts = selection.NameParts;
        var gender = selection.Gender;
        var edad = ResolveAge(record, attentionDate ?? generatedAt);
        var puesto = MedicalGenerationOptions.BuildJobTitle(record.CentroCosto);

        var evaluationWorksheet = GetWorksheet(
            workbook,
            "-EVALUACION_OCUPACIONAL_1-3",
            "-EVALUACION OCUPACIONAL 1-3");
        var occupationalWorksheet = GetWorksheet(
            workbook,
            "EVALUACION_OCUPACIONAL2-3",
            "EVALUACION OCUPACIONAL2-3");
        var certificateWorksheet = GetWorksheet(workbook, "CERTIFICADO");

        WriteEvaluationSheet(evaluationWorksheet, record, nameParts, evaluationType, attentionDate, edad, puesto, gender, company);
        WriteOccupationalSheet(occupationalWorksheet, puesto);

        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            InsertLogo(evaluationWorksheet, "B1:K1", logoPath);
            InsertLogo(certificateWorksheet, "A1:H1", logoPath);
        }

        workbook.CalculateMode = XLCalculateMode.Auto;
        workbook.SaveAs(outputPath);
    }

    private static IXLWorksheet GetWorksheet(XLWorkbook workbook, params string[] acceptedNames)
    {
        foreach (var acceptedName in acceptedNames)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault(
                candidate => string.Equals(candidate.Name, acceptedName, StringComparison.OrdinalIgnoreCase));

            if (worksheet is not null)
            {
                return worksheet;
            }
        }

        throw new InvalidDataException(
            $"La plantilla no contiene la hoja requerida. Nombres aceptados: {string.Join(", ", acceptedNames)}.");
    }

    private void WriteEvaluationSheet(
        IXLWorksheet worksheet,
        EmployeeRecord record,
        EmployeeNameParts nameParts,
        OccupationalEvaluationType evaluationType,
        DateTime? attentionDate,
        int? edad,
        string puesto,
        EmployeeGender gender,
        CompanyProfile company)
    {
        worksheet.Cell("B3").Value = $"{PhoneHeaderPrefix}{record.Movil ?? string.Empty}";
        WriteText(worksheet.Cell("B5"), company.Institution);
        WriteText(worksheet.Cell("Q5"), company.Ruc.Trim().TrimStart('\''));
        WriteText(worksheet.Cell("Z5"), company.Ciiu);
        WriteText(worksheet.Cell("AP5"), record.Identificacion);
        WriteText(worksheet.Cell("B8"), nameParts.PrimerApellido);
        WriteText(worksheet.Cell("S8"), nameParts.SegundoApellido);
        WriteText(worksheet.Cell("AH8"), nameParts.PrimerNombre);
        WriteText(worksheet.Cell("AR8"), nameParts.SegundoNombre);
        ClearCells(worksheet, "U13", "X13");
        worksheet.Cell(GetGenderMarkCell(gender, true)).Value = "X";

        if (record.FechaNacimiento.HasValue)
        {
            worksheet.Cell("Z13").Value = record.FechaNacimiento.Value.Year;
            worksheet.Cell("AC13").Value = record.FechaNacimiento.Value.Month;
            worksheet.Cell("AE13").Value = record.FechaNacimiento.Value.Day;
        }
        else
        {
            ClearCells(worksheet, "Z13", "AC13", "AE13");
        }

        if (edad.HasValue)
        {
            worksheet.Cell("AF13").Value = edad.Value;
        }
        else
        {
            worksheet.Cell("AF13").Clear(XLClearOptions.Contents);
        }

        WriteText(worksheet.Cell("J16"), puesto);
        WriteDate(worksheet.Cell("AH16"), attentionDate, "dd/MM/yyyy");

        WriteDate(worksheet.Cell("B18"), record.FechaIngreso);
        WriteDate(worksheet.Cell("AD18"), null);
        WriteDate(worksheet.Cell("AM18"), record.FechaLiquidacion);

        ClearCells(worksheet, "G19", "Z19", "AI19", "BA19");
        worksheet.Cell(GetEvaluationMarkCell(evaluationType)).Value = "X";
    }

    private static void WriteOccupationalSheet(IXLWorksheet worksheet, string puesto)
    {
        WriteText(worksheet.Cell("G2"), puesto);
    }

    private int? ResolveAge(EmployeeRecord record, DateTime generatedAt)
    {
        if (record.FechaNacimiento.HasValue)
        {
            return _ageCalculationService.CalculateAge(record.FechaNacimiento.Value.Date, generatedAt.Date);
        }

        return record.Edad;
    }

    private static void WriteDate(IXLCell cell, DateTime? value, string format = "yyyy/MM/dd")
    {
        if (!value.HasValue)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value.Value.Date;
        cell.Style.DateFormat.Format = format;
    }

    private static void WriteText(IXLCell cell, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.SetValue(value.Trim());
        cell.Style.NumberFormat.Format = "@";
    }

    private static string GetEvaluationMarkCell(OccupationalEvaluationType evaluationType)
    {
        return evaluationType switch
        {
            OccupationalEvaluationType.Preocupacional => "G19",
            OccupationalEvaluationType.Periodica => "Z19",
            OccupationalEvaluationType.Reintegro => "AI19",
            OccupationalEvaluationType.Retiro => "BA19",
            _ => "Z19"
        };
    }

    private static string GetGenderMarkCell(EmployeeGender gender, bool firstSheet)
    {
        return firstSheet switch
        {
            true when gender == EmployeeGender.Femenino => "X13",
            true => "U13",
            _ => string.Empty
        };
    }

    private static void ClearCells(IXLWorksheet worksheet, params string[] addresses)
    {
        foreach (var address in addresses)
        {
            worksheet.Cell(address).Clear(XLClearOptions.Contents);
        }
    }

    private static void InsertLogo(IXLWorksheet worksheet, string targetRangeAddress, string logoPath)
    {
        var target = worksheet.Range(targetRangeAddress);
        var width = Enumerable
            .Range(target.RangeAddress.FirstAddress.ColumnNumber, target.ColumnCount())
            .Sum(columnNumber => ColumnWidthToPixels(worksheet.Column(columnNumber).Width));
        var height = Enumerable
            .Range(target.RangeAddress.FirstAddress.RowNumber, target.RowCount())
            .Sum(rowNumber => PointsToPixels(worksheet.Row(rowNumber).Height));

        const int padding = 4;
        var picture = worksheet.AddPicture(logoPath, $"LogoEmpresa_{worksheet.Position}");
        var availableWidth = Math.Max(1, width - (padding * 2));
        var availableHeight = Math.Max(1, height - (padding * 2));
        var scale = Math.Min(
            availableWidth / (double)picture.OriginalWidth,
            availableHeight / (double)picture.OriginalHeight);
        var pictureWidth = Math.Max(1, (int)Math.Round(picture.OriginalWidth * scale));
        var pictureHeight = Math.Max(1, (int)Math.Round(picture.OriginalHeight * scale));
        var horizontalOffset = Math.Max(0, (width - pictureWidth) / 2);
        var verticalOffset = Math.Max(0, (height - pictureHeight) / 2);
        var topLeft = ResolveAnchor(worksheet, target, horizontalOffset, verticalOffset);
        var bottomRight = ResolveAnchor(
            worksheet,
            target,
            horizontalOffset + pictureWidth,
            verticalOffset + pictureHeight);

        picture.MoveTo(topLeft.Cell, topLeft.Offset, bottomRight.Cell, bottomRight.Offset);
    }

    private static (IXLCell Cell, Point Offset) ResolveAnchor(
        IXLWorksheet worksheet,
        IXLRange target,
        int horizontalPosition,
        int verticalPosition)
    {
        var firstColumn = target.RangeAddress.FirstAddress.ColumnNumber;
        var lastColumn = target.RangeAddress.LastAddress.ColumnNumber;
        var columnNumber = firstColumn;
        var columnOffset = Math.Max(0, horizontalPosition);

        while (columnNumber < lastColumn)
        {
            var columnWidth = ColumnWidthToPixels(worksheet.Column(columnNumber).Width);
            if (columnOffset < columnWidth)
            {
                break;
            }

            columnOffset -= columnWidth;
            columnNumber++;
        }

        var firstRow = target.RangeAddress.FirstAddress.RowNumber;
        var lastRow = target.RangeAddress.LastAddress.RowNumber;
        var rowNumber = firstRow;
        var rowOffset = Math.Max(0, verticalPosition);

        while (rowNumber < lastRow)
        {
            var rowHeight = PointsToPixels(worksheet.Row(rowNumber).Height);
            if (rowOffset < rowHeight)
            {
                break;
            }

            rowOffset -= rowHeight;
            rowNumber++;
        }

        return (worksheet.Cell(rowNumber, columnNumber), new Point(columnOffset, rowOffset));
    }

    private static int ColumnWidthToPixels(double width)
    {
        return Math.Max(1, (int)Math.Round((width * 7) + 5));
    }

    private static int PointsToPixels(double points)
    {
        return Math.Max(1, (int)Math.Round(points * 96d / 72d));
    }
}
