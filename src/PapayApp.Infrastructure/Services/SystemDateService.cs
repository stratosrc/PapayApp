using System.Globalization;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class SystemDateService : IDateService
{
    private static readonly CultureInfo MexicanSpanish = CultureInfo.GetCultureInfo("es-MX");

    public DateTime Today => DateTime.Today;

    public string GetWelcomeDateText()
    {
        return Today.ToString("dddd, d 'de' MMMM 'de' yyyy", MexicanSpanish);
    }
}
