using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class AgeCalculationServiceTests
{
    private readonly AgeCalculationService _service = new();

    [Fact]
    public void CalculateAge_WhenBirthdayAlreadyPassed_ReturnsAge()
    {
        var result = _service.CalculateAge(new DateTime(2000, 1, 10), new DateTime(2026, 7, 10));

        Assert.Equal(26, result);
    }

    [Fact]
    public void CalculateAge_WhenBirthdayHasNotPassedYet_SubtractsOne()
    {
        var result = _service.CalculateAge(new DateTime(2000, 12, 25), new DateTime(2026, 7, 10));

        Assert.Equal(25, result);
    }
}
