using PapayApp.Core.Models;

namespace PapayApp.Core.Configuration;

public static class MedicalTemplateOptions
{
    public static string GetTemplateFileName(OccupationalEvaluationType evaluationType)
    {
        return evaluationType switch
        {
            OccupationalEvaluationType.Preocupacional => "EVALUACION_INICIAL.xlsx",
            OccupationalEvaluationType.Periodica => "EVALUACION_PERIODICA.xlsx",
            OccupationalEvaluationType.Reintegro => "EVALUACION_REINTEGRO.xlsx",
            OccupationalEvaluationType.Retiro => "EVALUACION_RETIRO.xlsx",
            _ => throw new ArgumentOutOfRangeException(nameof(evaluationType), evaluationType, "Tipo de evaluacion no compatible.")
        };
    }
}
