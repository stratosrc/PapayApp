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

    public void Write(string templatePath, string outputPath, EmployeeGenerationSelection selection, OccupationalEvaluationType evaluationType, DateTime generatedAt)
    {
        using var workbook = new XLWorkbook(templatePath);
        var record = selection.Record;
        var nameParts = selection.NameParts;
        var gender = selection.Gender;
        var edad = ResolveAge(record, generatedAt);
        var puesto = MedicalGenerationOptions.BuildJobTitle(record.CentroCosto);

        WriteEvaluationSheet(workbook.Worksheet("-EVALUACION OCUPACIONAL 1-3"), record, nameParts, evaluationType, edad, puesto, gender);
        WriteOccupationalSheet(workbook.Worksheet("EVALUACION OCUPACIONAL2-3"), puesto);
        WriteCertificateSheet(workbook.Worksheet("CERTIFICADO"), record, nameParts, evaluationType, generatedAt, puesto, gender);

        workbook.SaveAs(outputPath);
    }

    private void WriteEvaluationSheet(
        IXLWorksheet worksheet,
        EmployeeRecord record,
        EmployeeNameParts nameParts,
        OccupationalEvaluationType evaluationType,
        int? edad,
        string puesto,
        EmployeeGender gender)
    {
        worksheet.Cell("B3").Value = $"{PhoneHeaderPrefix}{record.Movil ?? string.Empty}";
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
        worksheet.Cell("AH16").Clear(XLClearOptions.Contents);

        WriteDate(worksheet.Cell("B18"), record.FechaIngreso);
        WriteDate(worksheet.Cell("AD18"), null);
        WriteDate(worksheet.Cell("AM18"), record.FechaLiquidacion);

        ClearCells(worksheet, "G19", "Z19", "AI19", "BA19");
        worksheet.Cell(GetEvaluationMarkCell(evaluationType, true)).Value = "X";
    }

    private static void WriteOccupationalSheet(IXLWorksheet worksheet, string puesto)
    {
        WriteText(worksheet.Cell("G2"), puesto);
    }

    private void WriteCertificateSheet(
        IXLWorksheet worksheet,
        EmployeeRecord record,
        EmployeeNameParts nameParts,
        OccupationalEvaluationType evaluationType,
        DateTime generatedAt,
        string puesto,
        EmployeeGender gender)
    {
        worksheet.Cell("A2").Value = $"{PhoneHeaderPrefix}{record.Movil ?? string.Empty}";
        WriteText(worksheet.Cell("AC4"), record.Identificacion);
        WriteText(worksheet.Cell("A6"), nameParts.PrimerApellido);
        WriteText(worksheet.Cell("J6"), nameParts.SegundoApellido);
        WriteText(worksheet.Cell("Q6"), nameParts.PrimerNombre);
        WriteText(worksheet.Cell("X6"), nameParts.SegundoNombre);
        WriteText(worksheet.Cell("AD6"), MedicalGenerationOptions.GetCertificateGenderText(gender));
        WriteText(worksheet.Cell("AG6"), puesto);
        worksheet.Cell("K9").Value = generatedAt.Year;
        WriteTwoDigitNumber(worksheet.Cell("M9"), worksheet.Cell("N9"), generatedAt.Month);
        WriteTwoDigitNumber(worksheet.Cell("O9"), worksheet.Cell("P9"), generatedAt.Day);

        ClearCells(worksheet, "L11", "U11", "AC11", "AI11");
        worksheet.Cell(GetEvaluationMarkCell(evaluationType, false)).Value = "X";
    }

    private int? ResolveAge(EmployeeRecord record, DateTime generatedAt)
    {
        if (record.FechaNacimiento.HasValue)
        {
            return _ageCalculationService.CalculateAge(record.FechaNacimiento.Value.Date, generatedAt.Date);
        }

        return record.Edad;
    }

    private static void WriteDate(IXLCell cell, DateTime? value)
    {
        if (!value.HasValue)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value.Value.Date;
        cell.Style.DateFormat.Format = "yyyy/MM/dd";
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

    private static string GetEvaluationMarkCell(OccupationalEvaluationType evaluationType, bool firstSheet)
    {
        if (firstSheet)
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

        return evaluationType switch
        {
            OccupationalEvaluationType.Preocupacional => "L11",
            OccupationalEvaluationType.Periodica => "U11",
            OccupationalEvaluationType.Reintegro => "AC11",
            OccupationalEvaluationType.Retiro => "AI11",
            _ => "U11"
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

    private static void WriteTwoDigitNumber(IXLCell firstCell, IXLCell secondCell, int value)
    {
        var text = value.ToString("00");
        firstCell.Value = text[0].ToString();
        secondCell.Value = text[1].ToString();
    }
}
