using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.PreSprinkler;

// Admin-only wire shapes. Labels are worked out here, so the web app holds no rule about them.
public sealed record PreSprinklerContactDto(PreSprinklerContactRole Role, string? RoleLabel, string? Name,
    string? Company, string? Phone, string? Email, string? LicenseNumber);

public sealed record PreSprinklerSignalDto(string SignalType, string Description, int Weight);

public sealed record PreSprinklerLeadDto(
    Guid Id, string MarketSlug, string MarketName, string SourceId,
    string PermitNumber, string? PermitTypeLabel,
    PreSprinklerKind Kind, string KindLabel,
    SprinklerRequirement SprinklerRequirement, string RequirementLabel,
    string? Status, DateOnly? FiledOn, string? Description, string? ProjectName,
    string? Street, string? City, string? State, string? Zip,
    int? SquareFeet, bool SizeKnown, decimal? Valuation,
    string? RecordUrl, string? PortalUrl,
    SprinklerCheckStatus SprinklerCheckStatus, string SprinklerLabel, string? SprinklerTooltip,
    IReadOnlyList<SprinklerMatchDto> SprinklerMatches, DateOnly? SprinklerCheckedFrom, DateOnly? SprinklerCheckedAt,
    IReadOnlyList<string> Notes, int Score, PreSprinklerStanding Standing, string StandingLabel,
    DateTime FirstSeenAt, DateTime LastSeenAt,
    IReadOnlyList<PreSprinklerContactDto> Contacts,
    IReadOnlyList<PreSprinklerSignalDto>? Signals = null);

public sealed record PreSprinklerMarketDto(string Slug, string Name);
