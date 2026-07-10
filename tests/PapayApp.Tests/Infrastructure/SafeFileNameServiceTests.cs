using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class SafeFileNameServiceTests
{
    private readonly SafeFileNameService _service = new();

    [Fact]
    public void SanitizePathSegment_RemovesInvalidCharacters()
    {
        var result = _service.SanitizePathSegment("RIO:CENTRO/QUITO*", "fallback");

        Assert.Equal("RIO_CENTRO_QUITO_", result);
    }

    [Fact]
    public void EnsureUniqueFilePath_AppendsCounterWhenFileExists()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Evaluacion.xlsx"), "x");

            var result = _service.EnsureUniqueFilePath(directory.FullName, "Evaluacion", ".xlsx");

            Assert.EndsWith("Evaluacion (2).xlsx", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
