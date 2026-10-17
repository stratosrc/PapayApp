using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class HeaderNormalizerTests
{
    private readonly HeaderNormalizer _normalizer = new();

    [Theory]
    [InlineData("Identificacion", EmployeeField.Identificacion)]
    [InlineData("Identificación", EmployeeField.Identificacion)]
    [InlineData("Identificaci�n", EmployeeField.Identificacion)]
    [InlineData(" Cédula ", EmployeeField.Identificacion)]
    [InlineData("telefono", EmployeeField.Movil)]
    [InlineData("Móvil", EmployeeField.Movil)]
    [InlineData("Centro Costo", EmployeeField.CentroCosto)]
    [InlineData("Fecha de Liquidación", EmployeeField.FechaLiquidacion)]
    [InlineData("Ultimo Dia Laboral", EmployeeField.FechaLiquidacion)]
    public void ResolveField_RecognizesAliases(string rawHeader, EmployeeField expectedField)
    {
        var result = _normalizer.ResolveField(rawHeader);

        Assert.Equal(expectedField, result);
    }

    [Fact]
    public void Normalize_RemovesDiacriticsWhitespaceAndPunctuation()
    {
        var result = _normalizer.Normalize("  Identificación...  ");

        Assert.Equal("IDENTIFICACION", result);
    }
}
