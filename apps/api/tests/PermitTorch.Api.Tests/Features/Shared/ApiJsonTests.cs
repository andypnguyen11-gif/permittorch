using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Tests.Features.Shared;

public class ApiJsonTests
{
    [Theory]
    [InlineData(FireCategory.FireSprinkler, "FIRE_SPRINKLER")]
    [InlineData(FireCategory.KitchenSuppression, "KITCHEN_SUPPRESSION")]
    [InlineData(FireCategory.GeneralFireProtection, "GENERAL_FIRE_PROTECTION")]
    [InlineData(PermitStatusKind.Inspection, "INSPECTION")]
    [InlineData(PermitStatusKind.Unknown, "UNKNOWN")]
    [InlineData(PlanTier.Territory, "TERRITORY")]
    [InlineData(DigestFrequency.Weekly, "WEEKLY")]
    [InlineData(SavedLeadStatus.Contacted, "CONTACTED")]
    [InlineData(HealthStatus.Disabled, "DISABLED")]
    [InlineData(UserRole.SuperAdmin, "SUPER_ADMIN")]
    [InlineData(ParticipantRole.GeneralContractor, "GENERAL_CONTRACTOR")]
    public void Enums_serialize_to_locked_screaming_snake_strings(object value, string expected)
    {
        var json = JsonSerializer.Serialize(value, value.GetType(), ApiJson.Options);
        Assert.Equal($"\"{expected}\"", json);
    }

    [Fact]
    public void Enums_deserialize_from_locked_wire_strings()
    {
        Assert.Equal(SavedLeadStatus.Contacted,
            JsonSerializer.Deserialize<SavedLeadStatus>("\"CONTACTED\"", ApiJson.Options));
        Assert.Equal(DigestFrequency.None,
            JsonSerializer.Deserialize<DigestFrequency>("\"NONE\"", ApiJson.Options));
        Assert.Equal(PlanTier.Starter,
            JsonSerializer.Deserialize<PlanTier>("\"STARTER\"", ApiJson.Options));
    }

    [Fact]
    public void Dto_properties_serialize_camelCase_with_nulls_preserved()
    {
        var dto = new LeadSummaryDto(Guid.Empty, 91, "Fire Sprinkler", null, "Houston", "TX",
            FireCategory.FireSprinkler, null, PermitStatusKind.New, null, null, "why", true);
        var json = JsonSerializer.Serialize(dto, ApiJson.Options);
        Assert.Contains("\"score\":91", json);
        Assert.Contains("\"address\":null", json);
        Assert.Contains("\"filedDate\":null", json);
        Assert.Contains("\"estimatedValue\":null", json);
        Assert.Contains("\"category\":\"FIRE_SPRINKLER\"", json);
        Assert.Contains("\"isNew\":true", json);
    }

    [Fact]
    public void Wire_parses_and_names_round_trip()
    {
        Assert.True(Wire.TryParse<FireCategory>("VIOLATION_CORRECTION", out var category));
        Assert.Equal(FireCategory.ViolationCorrection, category);
        Assert.False(Wire.TryParse<FireCategory>("SPRINKLER", out _));
        Assert.Equal("FAILED", Wire.Name(PermitStatusKind.Failed));
        Assert.Equal("Kitchen Suppression", Wire.Title(FireCategory.KitchenSuppression));
    }

    [Fact]
    public void Error_response_serializes_to_locked_error_shape()
    {
        Assert.Equal("{\"error\":\"nope\"}", JsonSerializer.Serialize(new ErrorResponse("nope"), ApiJson.Options));
    }
}
