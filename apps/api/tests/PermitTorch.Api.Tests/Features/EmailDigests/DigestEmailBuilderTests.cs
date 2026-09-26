using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

public class DigestEmailBuilderTests
{
    private static readonly DigestLead Lead = new(94, FireCategory.FireSprinkler,
        "New warehouse sprinkler install", "Fire Sprinkler", "Houston", "TX",
        new DateTime(2026, 8, 18), 2_800_000m, "Houston");

    [Fact]
    public void Subject_matches_prd19_shape()
    {
        Assert.Equal("14 New Houston Fire Opportunities", DigestEmailBuilder.Subject(14, "Houston"));
        Assert.Equal("3 New Fire Opportunities", DigestEmailBuilder.Subject(3, null));
        Assert.Equal("Your 5 Free Houston Fire Opportunities", DigestEmailBuilder.SampleSubject(5, "Houston"));
    }

    [Fact]
    public void Html_contains_score_category_value_city_and_app_cta()
    {
        var html = DigestEmailBuilder.BuildHtml([Lead], "https://web.test");
        Assert.Contains("🔥 94 — Fire Sprinkler", html);
        Assert.Contains("$2,800,000", html);
        Assert.Contains("Houston, TX", html);
        Assert.Contains("href=\"https://web.test/app/leads\"", html);
        Assert.Contains("View All Opportunities", html);
    }

    [Fact]
    public void Sample_html_upsells_to_pricing()
    {
        var html = DigestEmailBuilder.BuildSampleHtml("Houston", [Lead], "https://web.test");
        Assert.Contains("href=\"https://web.test/pricing\"", html);
        Assert.Contains("Get Full Access", html);
    }

    [Fact]
    public void User_content_is_html_encoded()
    {
        var hostile = Lead with { Description = "<script>alert(1)</script>" };
        var html = DigestEmailBuilder.BuildHtml([hostile], "https://web.test");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
