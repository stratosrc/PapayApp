using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface IExcelImportService
{
    Task<IReadOnlyList<string>> GetWorksheetNamesAsync(string filePath, CancellationToken cancellationToken);

    Task<WorksheetImportPreview> ImportWorksheetAsync(string filePath, string worksheetName, CancellationToken cancellationToken);
}
