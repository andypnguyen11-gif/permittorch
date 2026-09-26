using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Auth;

/// <summary>Resolves the Firebase `sub` claim (uid) to an AppUser, auto-provisioning
/// AppUser + personal Organization + EmailPreference(None) on first authenticated
/// request (Architecture.md §5). Scoped: caches the lookup per request.</summary>
public sealed class CurrentUserService(AppDbContext db)
{
    private AppUser? _cached;

    public async Task<AppUser?> GetOrProvisionAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var sub = principal.FindFirstValue("sub");
        if (string.IsNullOrEmpty(sub)) return null;
        if (_cached?.FirebaseUid == sub) return _cached;

        var user = await db.AppUsers.Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.FirebaseUid == sub, ct);
        user ??= await ProvisionAsync(sub,
            principal.FindFirstValue("email") ?? $"{sub}@unknown.permittorch.invalid", ct);
        return _cached = user;
    }

    public async Task<AppUser> RequireAsync(ClaimsPrincipal principal, CancellationToken ct) =>
        await GetOrProvisionAsync(principal, ct)
            ?? throw new InvalidOperationException(
                "Authenticated principal has no sub claim; endpoint must require the User policy.");

    private async Task<AppUser> ProvisionAsync(string sub, string email, CancellationToken ct)
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = email };
        var user = new AppUser
        {
            Id = Guid.NewGuid(), FirebaseUid = sub, Email = email,
            OrganizationId = org.Id, Organization = org, Role = UserRole.Member,
        };
        var pref = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = DigestFrequency.None };
        db.Organizations.Add(org);
        db.AppUsers.Add(user);
        db.EmailPreferences.Add(pref);
        try
        {
            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateException)
        {
            // Concurrent first request won the app_users(firebase_uid) unique index — use theirs.
            db.ChangeTracker.Clear();
            return await db.AppUsers.Include(u => u.Organization).FirstAsync(u => u.FirebaseUid == sub, ct);
        }
    }
}
