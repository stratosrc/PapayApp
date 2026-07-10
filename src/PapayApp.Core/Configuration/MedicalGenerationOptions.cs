using PapayApp.Core.Models;

namespace PapayApp.Core.Configuration;

public static class MedicalGenerationOptions
{
    public const string DefaultGender = "HOMBRE";
    public const string DefaultJobTitle = "AUXILIAR DE LIMPIEZA";
    public const string MissingCostCenterFolderName = "SIN CENTRO DE COSTO";

    public static string BuildJobTitle(string? centroCosto)
    {
        return string.IsNullOrWhiteSpace(centroCosto)
            ? DefaultJobTitle
            : $"{DefaultJobTitle} / {centroCosto.Trim()}";
    }

    public static string GetVisibleFolderName(OccupationalEvaluationType evaluationType)
    {
        return evaluationType switch
        {
            OccupationalEvaluationType.Periodica => "PERIODICA",
            OccupationalEvaluationType.Preocupacional => "PREOCUPACIONAL",
            OccupationalEvaluationType.Reintegro => "REINTEGRO",
            OccupationalEvaluationType.Retiro => "RETIRO",
            _ => evaluationType.ToString().ToUpperInvariant()
        };
    }

    public static string GetBaseDirectoryFolderName(OccupationalEvaluationType evaluationType)
    {
        return evaluationType switch
        {
            OccupationalEvaluationType.Periodica => "EVALUACION MEDICA - PERIODICA",
            OccupationalEvaluationType.Preocupacional => "EVALUACION MEDICA - PREOCUPACIONAL",
            OccupationalEvaluationType.Reintegro => "EVALUACION MEDICA - REINTEGRO",
            OccupationalEvaluationType.Retiro => "EVALUACION MEDICA - RETIRO",
            _ => GetVisibleFolderName(evaluationType)
        };
    }

    public static string GetVisibleEvaluationName(OccupationalEvaluationType evaluationType)
    {
        return evaluationType switch
        {
            OccupationalEvaluationType.Periodica => "periodica",
            OccupationalEvaluationType.Preocupacional => "preocupacional",
            OccupationalEvaluationType.Reintegro => "de reintegro",
            OccupationalEvaluationType.Retiro => "de retiro",
            _ => evaluationType.ToString().ToLowerInvariant()
        };
    }
}
