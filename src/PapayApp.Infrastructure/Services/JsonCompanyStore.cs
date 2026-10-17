using System.Text.Json;
using PapayApp.Core.Models;
using PapayApp.Core.Services;

namespace PapayApp.Infrastructure.Services;

public sealed class JsonCompanyStore : ICompanyStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _storagePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonCompanyStore(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException("La ruta de almacenamiento de empresas es obligatoria.", nameof(storagePath));
        }

        _storagePath = storagePath;
    }

    public async Task<IReadOnlyList<CompanyProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAllCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CompanyProfile> AddAsync(CompanyProfile company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);

        var normalized = Normalize(company);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var companies = (await ReadAllCoreAsync(cancellationToken)).ToList();
            if (companies.Any(existing => string.Equals(NormalizeRuc(existing.Ruc), normalized.Ruc, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Ya existe una empresa registrada con ese RUC.");
            }

            companies.Add(normalized);
            await WriteAllCoreAsync(companies, cancellationToken);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string companyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var companies = (await ReadAllCoreAsync(cancellationToken)).ToList();
            companies.RemoveAll(company => string.Equals(company.Id, companyId, StringComparison.Ordinal));
            await WriteAllCoreAsync(companies, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<CompanyProfile>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<CompanyProfile>();
        }

        await using var stream = File.OpenRead(_storagePath);
        return await JsonSerializer.DeserializeAsync<List<CompanyProfile>>(stream, SerializerOptions, cancellationToken)
            ?? new List<CompanyProfile>();
    }

    private async Task WriteAllCoreAsync(IReadOnlyList<CompanyProfile> companies, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_storagePath}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, companies, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, _storagePath, true);
    }

    private static CompanyProfile Normalize(CompanyProfile company)
    {
        var institution = company.Institution.Trim();
        var ruc = NormalizeRuc(company.Ruc);
        var ciiu = company.Ciiu.Trim();

        if (string.IsNullOrWhiteSpace(institution) || string.IsNullOrWhiteSpace(ruc) || string.IsNullOrWhiteSpace(ciiu))
        {
            throw new InvalidOperationException("Institucion del sistema, RUC y CIIU son obligatorios.");
        }

        return new CompanyProfile
        {
            Id = string.IsNullOrWhiteSpace(company.Id) ? Guid.NewGuid().ToString("N") : company.Id,
            Institution = institution,
            Ruc = ruc,
            Ciiu = ciiu
        };
    }

    private static string NormalizeRuc(string? value)
    {
        return (value ?? string.Empty).Trim().TrimStart('\'').Trim();
    }
}
