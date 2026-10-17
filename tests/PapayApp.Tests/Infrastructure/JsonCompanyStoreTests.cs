using PapayApp.Core.Models;
using PapayApp.Infrastructure.Services;
using Xunit;

namespace PapayApp.Tests.Infrastructure;

public sealed class JsonCompanyStoreTests
{
    [Fact]
    public async Task AddAsync_PersistsNormalizedCompanyAndDeleteRemovesIt()
    {
        var directory = Directory.CreateTempSubdirectory();
        var storagePath = Path.Combine(directory.FullName, "companies.json");

        try
        {
            var store = new JsonCompanyStore(storagePath);
            var saved = await store.AddAsync(new CompanyProfile
            {
                Institution = "  MANLIM S.A.S  ",
                Ruc = "'0992424362001",
                Ciiu = " N8110.00 "
            });

            var reloadedStore = new JsonCompanyStore(storagePath);
            var reloaded = Assert.Single(await reloadedStore.GetAllAsync());
            Assert.Equal(saved.Id, reloaded.Id);
            Assert.Equal("MANLIM S.A.S", reloaded.Institution);
            Assert.Equal("0992424362001", reloaded.Ruc);
            Assert.Equal("N8110.00", reloaded.Ciiu);

            await reloadedStore.DeleteAsync(saved.Id);
            Assert.Empty(await reloadedStore.GetAllAsync());
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicateNormalizedRuc()
    {
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            var store = new JsonCompanyStore(Path.Combine(directory.FullName, "companies.json"));
            await store.AddAsync(CreateCompany("0992424362001"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.AddAsync(CreateCompany("'0992424362001")));

            Assert.Contains("RUC", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static CompanyProfile CreateCompany(string ruc)
    {
        return new CompanyProfile
        {
            Institution = "Empresa de prueba",
            Ruc = ruc,
            Ciiu = "N8110.00"
        };
    }
}
