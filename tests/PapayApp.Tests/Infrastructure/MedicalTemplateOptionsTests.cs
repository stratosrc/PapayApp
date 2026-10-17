using PapayApp.Core.Configuration;
using PapayApp.Core.Models;
using PapayApp.Tests.Helpers;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class MedicalTemplateOptionsTests
{
    [Theory]
    [InlineData(OccupationalEvaluationType.Preocupacional, "EVALUACION_INICIAL.xlsx")]
    [InlineData(OccupationalEvaluationType.Periodica, "EVALUACION_PERIODICA.xlsx")]
    [InlineData(OccupationalEvaluationType.Reintegro, "EVALUACION_REINTEGRO.xlsx")]
    [InlineData(OccupationalEvaluationType.Retiro, "EVALUACION_RETIRO.xlsx")]
    public void GetTemplateFileName_ReturnsExpectedTemplate(
        OccupationalEvaluationType evaluationType,
        string expectedFileName)
    {
        Assert.Equal(expectedFileName, MedicalTemplateOptions.GetTemplateFileName(evaluationType));
        Assert.True(File.Exists(TestPaths.MedicalTemplatePath(evaluationType)));
    }
}
