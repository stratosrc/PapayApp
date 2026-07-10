using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class SystemDateServiceTests
{
    [Fact]
    public void WelcomeDateText_ReturnsReadableSpanishDate()
    {
        var service = new SystemDateService();

        var text = service.GetWelcomeDateText();

        Assert.Contains(DateTime.Today.Year.ToString(), text);
    }
}
