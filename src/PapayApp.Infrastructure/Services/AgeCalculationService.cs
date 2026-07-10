using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class AgeCalculationService : IAgeCalculationService
{
    public int CalculateAge(DateTime birthDate, DateTime referenceDate)
    {
        var age = referenceDate.Year - birthDate.Year;
        if (birthDate.Date > referenceDate.Date.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
