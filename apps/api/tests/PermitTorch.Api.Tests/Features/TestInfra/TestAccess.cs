using Microsoft.Extensions.Options;
using PermitTorch.Api.Features.Auth;

namespace PermitTorch.Api.Tests.Features.TestInfra;

/// <summary>Access options for services built by hand in tests: the paid path (default) or the free pilot.</summary>
public static class TestAccess
{
    public static IOptions<AccessOptions> Paid => Options.Create(new AccessOptions());
    public static IOptions<AccessOptions> Free => Options.Create(new AccessOptions { FreeForAll = true });
    public static IOptions<AccessOptions> For(bool freeForAll) => freeForAll ? Free : Paid;
}
