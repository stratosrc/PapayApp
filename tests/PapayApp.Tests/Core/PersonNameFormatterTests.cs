using PapayApp.Core.Services;
using Xunit;

namespace PapayApp.Tests.Core;

public sealed class PersonNameFormatterTests
{
    [Theory]
    [InlineData("JUAN GARCÍA", "Juan García")]
    [InlineData("juan garcía", "Juan García")]
    [InlineData("mArÍA jOsÉ muÑOz", "María José Muñoz")]
    [InlineData("  ANA   MARÍA  ", "Ana María")]
    public void ToTitleCase_NormalizesCasingAndPreservesDiacritics(string input, string expected)
    {
        Assert.Equal(expected, PersonNameFormatter.ToTitleCase(input));
    }
}
