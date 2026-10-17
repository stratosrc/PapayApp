using PapayApp.Infrastructure.Services;
using PapayApp.Tests.Helpers;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class LogoImageValidatorTests
{
    [Fact]
    public void IsValid_AcceptsPngAndJpegAndRejectsInvalidFiles()
    {
        var directory = Directory.CreateTempSubdirectory();
        var jpegPath = Path.Combine(directory.FullName, "logo.jpeg");
        var fakePngPath = Path.Combine(directory.FullName, "fake.png");
        var fakeJpegPath = Path.Combine(directory.FullName, "fake.jpg");

        try
        {
            File.WriteAllBytes(jpegPath, TestPaths.TinyJpegBytes);
            File.WriteAllText(fakePngPath, "not a png");
            File.WriteAllText(fakeJpegPath, "not a jpeg");

            Assert.True(LogoImageValidator.IsValid(TestPaths.DiplomaIconPath));
            Assert.True(LogoImageValidator.IsValid(jpegPath));
            Assert.False(LogoImageValidator.IsValid(fakePngPath));
            Assert.False(LogoImageValidator.IsValid(fakeJpegPath));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
