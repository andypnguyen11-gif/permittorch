using Microsoft.AspNetCore.Authorization;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Auth;

public sealed class SuperAdminRequirement : IAuthorizationRequirement;

/// <summary>Role check against the DATABASE row (PRD §58: role-based admin
/// authorization server-side), never a token claim a client could mint.</summary>
public sealed class SuperAdminHandler(CurrentUserService currentUser)
    : AuthorizationHandler<SuperAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SuperAdminRequirement requirement)
    {
        var user = await currentUser.GetOrProvisionAsync(context.User, CancellationToken.None);
        if (user is { Role: UserRole.SuperAdmin })
            context.Succeed(requirement);
    }
}
