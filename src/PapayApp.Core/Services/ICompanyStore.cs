using PapayApp.Core.Models;

namespace PapayApp.Core.Services;

public interface ICompanyStore
{
    Task<IReadOnlyList<CompanyProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CompanyProfile> AddAsync(CompanyProfile company, CancellationToken cancellationToken = default);

    Task DeleteAsync(string companyId, CancellationToken cancellationToken = default);
}
