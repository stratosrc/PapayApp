namespace PapayApp.Core.Services;

public interface IAgeCalculationService
{
    int CalculateAge(DateTime birthDate, DateTime referenceDate);
}
