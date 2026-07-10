using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class NameSplitServiceTests
{
    private readonly NameSplitService _service = new();

    [Fact]
    public void Split_SplitsNamesAndSurnamesIntoFirstAndRest()
    {
        var result = _service.Split("ROSALINDA ELIZABETH", "MOLINA TORRES");

        Assert.Equal("ROSALINDA", result.PrimerNombre);
        Assert.Equal("ELIZABETH", result.SegundoNombre);
        Assert.Equal("MOLINA", result.PrimerApellido);
        Assert.Equal("TORRES", result.SegundoApellido);
    }

    [Fact]
    public void Split_WithSingleTerm_LeavesSecondPartEmpty()
    {
        var result = _service.Split("JAIRO", "TOCTAQUIZA");

        Assert.Equal("JAIRO", result.PrimerNombre);
        Assert.Null(result.SegundoNombre);
        Assert.Equal("TOCTAQUIZA", result.PrimerApellido);
        Assert.Null(result.SegundoApellido);
    }
}
