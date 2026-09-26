using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermitTorch.Api.Features.Shared;

/// <summary>Single source of truth for the LOCKED wire format (master §6/§7):
/// camelCase properties, enums as SCREAMING_SNAKE strings.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null; // MarketStats.byCategory keys are already wire names
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
    }
}
