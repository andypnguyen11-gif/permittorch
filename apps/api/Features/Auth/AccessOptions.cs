namespace PermitTorch.Api.Features.Auth;

/// <summary>Bound from "Access" (env <c>Access__FreeForAll</c>). <see cref="FreeForAll"/> makes
/// PermitTorch free during the pilot: every signed-in user is entitled to every active market and
/// to the top plan, and checkout is refused. Off by default, so the paid path is unchanged unless
/// it is set. Turning it off restores subscription-based entitlement; no data is changed by it.</summary>
public sealed class AccessOptions
{
    public const string Section = "Access";

    public bool FreeForAll { get; set; }
}
