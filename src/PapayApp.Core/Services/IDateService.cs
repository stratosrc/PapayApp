namespace PapayApp.Core.Services;

public interface IDateService
{
    DateTime Today { get; }
    string GetWelcomeDateText();
}
