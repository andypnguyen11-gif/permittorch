using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Features.Feedback;
using PermitTorch.Api.Tests.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Feedback;

sealed class ListLogger<T> : ILogger<T>
{
    public readonly List<(LogLevel Level, string Text)> Lines = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Lines.Add((logLevel, formatter(state, exception)));
}

[Collection("api")]
public class FeedbackNotifierTests(ApiFactory factory)
{
    private const string Owner = "owner@test";
    private const string Secret = "the secret message text";

    private async Task<(FeedbackSubmission Row, AppUser User)> SeedAsync(DateTime createdAt, DateTime? notifiedAt = null)
    {
        var sub = $"fb-{Guid.NewGuid():N}";
        var (org, user, _) = TestSeed.User(sub, $"{sub}@example.com");
        var row = new FeedbackSubmission
        {
            Id = Guid.NewGuid(), CreatedAt = createdAt, UserId = user.Id, Type = FeedbackType.Problem,
            Message = Secret, PagePath = "/app/leads", NotifiedAt = notifiedAt,
        };
        await factory.SeedAsync(db =>
        {
            db.Organizations.Add(org);
            db.AppUsers.Add(user);
            db.FeedbackSubmissions.Add(row);
        });
        user.Organization = org;
        return (row, user);
    }

    private async Task<T> WithNotifierAsync<T>(RecordingResend resend, Func<FeedbackNotifier, AppDbContext, Task<T>> act,
        string notifyTo = Owner, ListLogger<FeedbackNotifier>? logger = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = new FeedbackNotifier(db, resend,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "leads@test", FeedbackNotifyEmail = notifyTo }),
            logger ?? new ListLogger<FeedbackNotifier>());
        return await act(notifier, db);
    }

    private Task<DateTime?> NotifiedAtAsync(Guid id) =>
        factory.QueryAsync(db => db.FeedbackSubmissions.Where(f => f.Id == id).Select(f => f.NotifiedAt).SingleAsync());

    private static Task<bool> TrySend(FeedbackNotifier notifier, AppDbContext db, FeedbackSubmission row, AppUser user) =>
        db.FeedbackSubmissions.SingleAsync(f => f.Id == row.Id)
            .ContinueWith(t => notifier.TrySendAsync(t.Result, user, CancellationToken.None)).Unwrap();

    [Fact]
    public async Task TrySend_sends_and_marks_notified()
    {
        var (row, user) = await SeedAsync(DateTime.UtcNow);
        var resend = new RecordingResend();

        var sent = await WithNotifierAsync(resend, (n, db) => TrySend(n, db, row, user));

        Assert.True(sent);
        var message = Assert.Single(resend.Messages);
        Assert.Equal(Owner, message.To);
        Assert.Equal($"feedback:{row.Id}", message.IdempotencyKey);
        Assert.NotNull(await NotifiedAtAsync(row.Id));
    }

    [Fact]
    public async Task TrySend_with_no_notify_address_sends_nothing_and_logs_without_message()
    {
        var (row, user) = await SeedAsync(DateTime.UtcNow);
        var resend = new RecordingResend();
        var logger = new ListLogger<FeedbackNotifier>();

        var sent = await WithNotifierAsync(resend, (n, db) => TrySend(n, db, row, user), notifyTo: "", logger: logger);

        Assert.False(sent);
        Assert.Empty(resend.Messages);
        var line = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal($"Feedback {row.Id} saved but FEEDBACK_NOTIFY_EMAIL is unset", line.Text);
        Assert.Null(await NotifiedAtAsync(row.Id));
    }

    [Fact]
    public async Task TrySend_resend_failure_leaves_row_pending_and_logs_without_message()
    {
        var (row, user) = await SeedAsync(DateTime.UtcNow);
        var resend = new RecordingResend { OnSend = _ => throw new HttpRequestException("boom", null, HttpStatusCode.InternalServerError) };
        var logger = new ListLogger<FeedbackNotifier>();

        var sent = await WithNotifierAsync(resend, (n, db) => TrySend(n, db, row, user), logger: logger);

        Assert.False(sent);
        Assert.Null(await NotifiedAtAsync(row.Id));
        Assert.Contains(logger.Lines, l => l.Text.Contains(row.Id.ToString()));
        Assert.DoesNotContain(logger.Lines, l => l.Text.Contains(Secret));
    }

    [Fact]
    public async Task Retry_sends_pending_rows_older_than_grace()
    {
        var now = DateTime.UtcNow;
        var (old, _) = await SeedAsync(now.AddMinutes(-6));
        var (fresh, _) = await SeedAsync(now.AddMinutes(-4));
        var (done, _) = await SeedAsync(now.AddMinutes(-10), notifiedAt: now.AddMinutes(-9));
        var resend = new RecordingResend();

        await WithNotifierAsync(resend, (n, _) => n.RetryPendingAsync(now, CancellationToken.None));

        var keys = resend.Messages.Select(m => m.IdempotencyKey).ToList();
        Assert.Contains($"feedback:{old.Id}", keys);
        Assert.DoesNotContain($"feedback:{fresh.Id}", keys);
        Assert.DoesNotContain($"feedback:{done.Id}", keys);
        Assert.NotNull(await NotifiedAtAsync(old.Id));
        Assert.Null(await NotifiedAtAsync(fresh.Id));
    }

    [Fact]
    public async Task Retry_409_leaves_row_pending_and_continues()
    {
        var now = DateTime.UtcNow;
        var (blocked, _) = await SeedAsync(now.AddMinutes(-30));
        var (next, _) = await SeedAsync(now.AddMinutes(-20));
        var resend = new RecordingResend
        {
            OnSend = m => m.IdempotencyKey == $"feedback:{blocked.Id}"
                ? throw new HttpRequestException("conflict", null, HttpStatusCode.Conflict)
                : Task.CompletedTask,
        };

        await WithNotifierAsync(resend, (n, _) => n.RetryPendingAsync(now, CancellationToken.None));

        Assert.Null(await NotifiedAtAsync(blocked.Id));
        Assert.NotNull(await NotifiedAtAsync(next.Id));
    }

    [Fact]
    public async Task Retry_does_nothing_when_notify_address_unset()
    {
        var now = DateTime.UtcNow;
        var (row, _) = await SeedAsync(now.AddMinutes(-6));
        var resend = new RecordingResend();

        await WithNotifierAsync(resend, (n, _) => n.RetryPendingAsync(now, CancellationToken.None), notifyTo: "");

        Assert.Empty(resend.Messages);
        Assert.Null(await NotifiedAtAsync(row.Id));
    }

    [Fact]
    public async Task Retry_after_send_succeeded_but_save_failed_reuses_key_and_payload()
    {
        // Resend delivered the first email, but our side never recorded it: the row stays pending.
        var (row, user) = await SeedAsync(DateTime.UtcNow);
        EmailMessage? delivered = null;
        var first = new RecordingResend
        {
            OnSend = m => { delivered = m; throw new HttpRequestException("lost after delivery"); },
        };
        await WithNotifierAsync(first, (n, db) => TrySend(n, db, row, user));
        Assert.NotNull(delivered);

        // The next hourly sweep, once the row is past the grace period.
        var retry = new RecordingResend();
        await WithNotifierAsync(retry, (n, _) => n.RetryPendingAsync(DateTime.UtcNow.AddMinutes(6), CancellationToken.None));

        var resent = Assert.Single(retry.Messages, m => m.IdempotencyKey == $"feedback:{row.Id}");
        Assert.Equal(delivered! with { }, resent with { });
    }
}
