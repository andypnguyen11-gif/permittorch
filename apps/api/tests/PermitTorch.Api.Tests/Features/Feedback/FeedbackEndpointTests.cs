using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Feedback;

public sealed class FeedbackEndpointTests : IAsyncLifetime
{
    private bool _failSends;
    private readonly RecordingResend _resend;
    private readonly ApiFactory _factory;

    public FeedbackEndpointTests()
    {
        _resend = new RecordingResend
        {
            OnSend = _ => _failSends ? throw new HttpRequestException("resend down") : Task.CompletedTask,
        };
        _factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { ["FEEDBACK_NOTIFY_EMAIL"] = "owner@test" },
            TestServices = services => services.AddSingleton<ResendEmailClient>(_resend),
        };
    }

    public Task InitializeAsync() => _factory.InitializeAsync();
    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static string NewSub() => $"fb-{Guid.NewGuid():N}";

    private HttpClient ClientFor(string sub) => _factory.CreateClientFor(sub, $"{sub}@example.com");

    private Task<List<FeedbackSubmission>> RowsForAsync(string sub) =>
        _factory.QueryAsync(db => db.FeedbackSubmissions.Include(f => f.User)
            .Where(f => f.User.FirebaseUid == sub).ToListAsync());

    [Fact]
    public async Task Without_login_returns_401_and_stores_nothing()
    {
        var before = await _factory.QueryAsync(db => db.FeedbackSubmissions.CountAsync());

        var response = await _factory.CreateClient().PostAsync("/api/feedback",
            Json("""{"type":"IDEA","message":"hello"}"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, await _factory.QueryAsync(db => db.FeedbackSubmissions.CountAsync()));
    }

    [Fact]
    public async Task Valid_submission_returns_202_saves_row_for_the_login_and_emails()
    {
        var sub = NewSub();

        var response = await ClientFor(sub).PostAsync("/api/feedback",
            Json("""{"type":"DATA_REQUEST","message":"  Add Austin  ","pagePath":"/app/leads"}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var row = Assert.Single(await RowsForAsync(sub));
        Assert.Equal(FeedbackType.DataRequest, row.Type);
        Assert.Equal("Add Austin", row.Message);
        Assert.Equal("/app/leads", row.PagePath);
        Assert.NotNull(row.NotifiedAt);
        var email = Assert.Single(_resend.Messages, m => m.IdempotencyKey == $"feedback:{row.Id}");
        Assert.Equal("owner@test", email.To);
        Assert.Equal($"{sub}@example.com", email.ReplyTo);
    }

    [Fact]
    public async Task Body_user_id_and_email_are_ignored()
    {
        var sub = NewSub();
        var otherUserId = Guid.NewGuid();

        var response = await ClientFor(sub).PostAsync("/api/feedback",
            Json($$"""{"type":"IDEA","message":"hi","userId":"{{otherUserId}}","email":"x@evil.test"}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var row = Assert.Single(await RowsForAsync(sub));
        Assert.NotEqual(otherUserId, row.UserId);
        var email = Assert.Single(_resend.Messages, m => m.IdempotencyKey == $"feedback:{row.Id}");
        Assert.Equal($"{sub}@example.com", email.ReplyTo);
    }

    [Theory]
    [InlineData("""{"message":"hi"}""")]
    [InlineData("""{"type":"BUG","message":"hi"}""")]
    [InlineData("""{"type":"IDEA"}""")]
    [InlineData("""{"type":"IDEA","message":"   "}""")]
    public async Task Invalid_body_returns_400(string body)
    {
        var sub = NewSub();

        var response = await ClientFor(sub).PostAsync("/api/feedback", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await RowsForAsync(sub));
    }

    [Fact]
    public async Task Message_over_5000_characters_returns_400()
    {
        var sub = NewSub();
        var message = new string('m', 5001);

        var response = await ClientFor(sub).PostAsync("/api/feedback",
            Json($$"""{"type":"IDEA","message":"{{message}}"}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Bad_page_path_is_stored_null_and_message_accepted()
    {
        var sub = NewSub();

        var response = await ClientFor(sub).PostAsync("/api/feedback",
            Json("""{"type":"PROBLEM","message":"broken","pagePath":"//evil.test"}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(Assert.Single(await RowsForAsync(sub)).PagePath);
    }

    [Fact]
    public async Task Email_failure_still_returns_202_and_leaves_row_pending()
    {
        var sub = NewSub();
        _failSends = true;

        var response = await ClientFor(sub).PostAsync("/api/feedback",
            Json("""{"type":"OTHER","message":"still saved"}"""));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(Assert.Single(await RowsForAsync(sub)).NotifiedAt);
    }

    [Fact]
    public async Task Sixth_request_in_window_returns_429_and_other_users_are_unaffected()
    {
        var client = ClientFor(NewSub());
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted,
                (await client.PostAsync("/api/feedback", Json("""{"type":"IDEA","message":"again"}"""))).StatusCode);

        var sixth = await client.PostAsync("/api/feedback", Json("""{"type":"IDEA","message":"again"}"""));
        var other = await ClientFor(NewSub()).PostAsync("/api/feedback", Json("""{"type":"IDEA","message":"mine"}"""));

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
    }
}
