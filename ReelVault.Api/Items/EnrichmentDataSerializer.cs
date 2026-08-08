using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Items;

// Pure, DB-free JSON mapping between EnrichmentData and the EnrichmentData jsonb column text -
// same pattern as CategoryDataSerializer, kept separate from EF for direct unit testing.
public static class EnrichmentDataSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(EnrichmentData data) => JsonSerializer.Serialize(data, Options);

    public static EnrichmentData? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<EnrichmentData>(json, Options);
}
