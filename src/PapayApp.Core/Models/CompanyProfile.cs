using System.Text.Json.Serialization;

namespace PapayApp.Core.Models;

public sealed class CompanyProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required string Institution { get; init; }

    public required string Ruc { get; init; }

    public required string Ciiu { get; init; }

    [JsonIgnore]
    public string DisplayName => $"{Institution} - {Ruc}";
}
