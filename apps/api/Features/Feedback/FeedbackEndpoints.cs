using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Feedback;

// No user id or email here: the sender is always the signed-in account.
public sealed record FeedbackRequestBody(FeedbackType? Type, string? Message, string? PagePath);

public static class FeedbackEndpoints
{
    public const string RateLimitPolicy = "feedback";
    public const int MaxMessageLength = 5000;

    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/feedback", Submit)
            .RequireAuthorization("User")
            .RequireRateLimiting(RateLimitPolicy);
        return endpoints;
    }

    private static async Task<IResult> Submit(FeedbackRequestBody body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, FeedbackNotifier notifier, CancellationToken ct)
    {
        if (body.Type is not { } type) return ApiErrors.BadRequest("type is required");
        // Postgres text cannot hold NUL; drop it rather than fail on insert.
        var message = body.Message?.Replace("\0", "").Trim() ?? "";
        if (message.Length is 0 or > MaxMessageLength)
            return ApiErrors.BadRequest($"message is required (max {MaxMessageLength} characters)");

        var user = await currentUser.RequireAsync(http.User, ct);
        var feedback = new FeedbackSubmission
        {
            Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UserId = user.Id, Type = type,
            Message = message, PagePath = FeedbackPagePath.Normalize(body.PagePath),
        };
        db.FeedbackSubmissions.Add(feedback);
        await db.SaveChangesAsync(ct);

        // Saved first: a failed email is retried hourly and never fails the request. Not tied to
        // the request's lifetime, so a closed tab does not cancel the send (HttpClient times out).
        await notifier.TrySendAsync(feedback, user, CancellationToken.None);
        return Results.Accepted();
    }
}
