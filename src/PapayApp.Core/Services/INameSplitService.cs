using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface INameSplitService
{
    EmployeeNameParts Split(string? nombres, string? apellidos);
}
