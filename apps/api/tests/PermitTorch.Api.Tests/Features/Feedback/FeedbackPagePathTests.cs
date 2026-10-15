using PermitTorch.Api.Features.Feedback;

namespace PermitTorch.Api.Tests.Features.Feedback;

public class FeedbackPagePathTests
{
    [Theory]
    [InlineData("/app/leads/123")]
    [InlineData("/pricing")]
    [InlineData("/")]
    public void Keeps_a_plain_path(string path) =>
        Assert.Equal(path, FeedbackPagePath.Normalize(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app/leads")]
    [InlineData("https://x")]
    [InlineData("//evil")]
    [InlineData("/\\evil")]
    [InlineData("/app?x=1")]
    [InlineData("/app#a")]
    [InlineData("/app\u0007")]
    [InlineData("/app\n")]
    public void Drops_anything_but_a_plain_path(string? path) =>
        Assert.Null(FeedbackPagePath.Normalize(path));

    [Fact]
    public void Keeps_500_characters_and_drops_501()
    {
        var max = "/" + new string('a', FeedbackPagePath.MaxLength - 1);
        Assert.Equal(max, FeedbackPagePath.Normalize(max));
        Assert.Null(FeedbackPagePath.Normalize(max + "a"));
    }
}
