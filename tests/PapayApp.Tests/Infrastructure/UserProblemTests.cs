using PapayApp.UI.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;
public sealed class UserProblemTests
{
    [Fact]
    public void LockedFile_GivesRecoveryActionAndPreservesOriginalDetail()
    {
        var result = UserProblem.Describe(new IOException("archivo bloqueado: lote.pdf"), "guardar el PDF");
        Assert.Contains("Cierra el PDF o Excel", result);
        Assert.Contains("archivo bloqueado: lote.pdf", result);
        Assert.Contains("guardar el PDF", result);
    }
    [Fact]
    public void TextDoesNotFit_ExplainsHowToRepairTemplate()
    {
        var result = UserProblem.Describe(new InvalidDataException("El texto de Nombre no cabe en el área configurada."), "generar");
        Assert.Contains("amplía el rectángulo", result);
        Assert.Contains("guarda la plantilla", result);
    }
    [Fact]
    public void MissingInput_ExplainsReselectingFile()
    {
        Assert.Contains("Vuelve a seleccionarlo", UserProblem.Describe(new FileNotFoundException("firma.png"), "cargar"));
    }
}
