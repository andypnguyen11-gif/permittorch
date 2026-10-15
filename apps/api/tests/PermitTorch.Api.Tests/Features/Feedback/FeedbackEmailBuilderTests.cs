using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Feedback;

namespace PermitTorch.Api.Tests.Features.Feedback;

public class FeedbackEmailBuilderTests
{
    private const string Owner = "owner@example.com";

    private static AppUser User(string orgName = "Acme Fire") => new()
    {
        Id = Guid.NewGuid(), FirebaseUid = "uid", Email = "pat@acme.test",
        Organization = new Organization { Id = Guid.NewGuid(), Name = orgName },
    };

    private static FeedbackSubmission Row(string message, FeedbackType type = FeedbackType.Idea,
        string? pagePath = null) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        CreatedAt = new DateTime(2026, 10, 15, 21, 4, 59, DateTimeKind.Utc),
        Type = type, Message = message, PagePath = pagePath,
    };

    [Fact]
    public void Build_addresses_owner_with_reply_to_sender()
    {
        var row = Row("Hello");
        var email = FeedbackEmailBuilder.Build(row, User(), Owner);

        Assert.Equal(Owner, email.To);
        Assert.Equal("pat@acme.test", email.ReplyTo);
        Assert.Null(email.UnsubscribeUrl);
        Assert.Equal($"feedback:{row.Id}", email.IdempotencyKey);
        Assert.Equal(email.IdempotencyKey, FeedbackEmailBuilder.IdempotencyKey(row.Id));
    }

    [Fact]
    public void Subject_uses_type_label_and_first_60_characters()
    {
        var message = new string('x', 60) + new string('y', 20);
        var email = FeedbackEmailBuilder.Build(Row(message, FeedbackType.DataRequest), User(), Owner);

        Assert.Equal("[Feedback · Data request] " + new string('x', 60) + "…", email.Subject);
    }

    [Fact]
    public void Subject_of_a_short_message_has_no_ellipsis()
    {
        var email = FeedbackEmailBuilder.Build(Row("Add Austin please"), User(), Owner);

        Assert.Equal("[Feedback · Idea] Add Austin please", email.Subject);
    }

    [Fact]
    public void Subject_collapses_newlines()
    {
        var email = FeedbackEmailBuilder.Build(Row("Line one\r\nLine two\nLine three"), User(), Owner);

        Assert.Equal("[Feedback · Idea] Line one Line two Line three", email.Subject);
    }

    [Fact]
    public void Subject_cut_does_not_split_surrogate_pair()
    {
        var message = new string('a', 59) + "😀" + "tail text";
        var subject = FeedbackEmailBuilder.Build(Row(message), User(), Owner).Subject;

        var beforeEllipsis = subject[..^1];
        Assert.EndsWith("…", subject);
        Assert.False(char.IsHighSurrogate(beforeEllipsis[^1]));
    }

    [Fact]
    public void Body_encodes_user_input()
    {
        var html = FeedbackEmailBuilder.Build(Row("<script>alert(1)</script>"), User("A&B"), Owner).Html;

        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("A&amp;B", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void Body_keeps_line_breaks()
    {
        var html = FeedbackEmailBuilder.Build(Row("a<\nb"), User(), Owner).Html;

        Assert.Contains("a&lt;<br>b", html);
    }

    [Fact]
    public void Body_includes_page_path_only_when_present()
    {
        Assert.Contains("/app/leads/123",
            FeedbackEmailBuilder.Build(Row("m", pagePath: "/app/leads/123"), User(), Owner).Html);
        Assert.DoesNotContain("Page",
            FeedbackEmailBuilder.Build(Row("m"), User(), Owner).Html);
    }

    [Fact]
    public void Build_is_identical_across_timestamp_precision()
    {
        var first = Row("Same message");
        var second = Row("Same message");
        second.CreatedAt = first.CreatedAt.AddTicks(5);
        var user = User();

        Assert.Equal(FeedbackEmailBuilder.Build(first, user, Owner), FeedbackEmailBuilder.Build(second, user, Owner));
    }

    [Theory]
    [InlineData(FeedbackType.Idea, "Idea")]
    [InlineData(FeedbackType.Problem, "Problem")]
    [InlineData(FeedbackType.DataRequest, "Data request")]
    [InlineData(FeedbackType.Other, "Other")]
    public void Type_labels(FeedbackType type, string label) =>
        Assert.Equal(label, FeedbackEmailBuilder.TypeLabel(type));
}
