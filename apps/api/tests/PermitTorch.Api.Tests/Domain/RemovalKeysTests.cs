using System;
using PermitTorch.Api.Domain.Removals;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class RemovalKeysTests
{
    [Theory]
    [InlineData("(480) 555-0142", "4805550142")]
    [InlineData("480-555-0142", "4805550142")]
    [InlineData("1-480-555-0142", "4805550142")]
    [InlineData("+1 (480) 555-0142", "4805550142")]
    [InlineData("(480) 555-0142 x12", "4805550142")]
    [InlineData("1 480 555 0142 ext 7", "4805550142")]
    [InlineData("480.555.0142", "4805550142")]
    public void A_phone_is_compared_by_its_ten_digits(string value, string key)
        => Assert.Equal(key, RemovalKeys.Phone(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("555-0142")]
    [InlineData("call the office")]
    public void A_value_without_ten_digits_is_no_phone(string? value)
        => Assert.Null(RemovalKeys.Phone(value));

    [Theory]
    [InlineData("Office@Example.COM", "office@example.com")]
    [InlineData("  office@example.com ", "office@example.com")]
    public void An_email_is_compared_trimmed_and_in_lower_case(string value, string key)
        => Assert.Equal(key, RemovalKeys.Email(value));

    [Theory]
    [InlineData("John Smith", "JOHN SMITH")]
    [InlineData("  john   smith ", "JOHN SMITH")]
    [InlineData("John\tSmith", "JOHN SMITH")]
    [InlineData("José Núñez", "JOSÉ NÚÑEZ")]
    [InlineData("JOSÉ NÚÑEZ", "JOSÉ NÚÑEZ")]
    public void A_name_is_compared_without_case_or_extra_space(string value, string key)
        => Assert.Equal(key, RemovalKeys.Name(value));

    [Fact]
    public void A_name_in_another_order_is_another_name()
        => Assert.NotEqual(RemovalKeys.Name("Smith, John"), RemovalKeys.Name("John Smith"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_value_has_no_key(string? value)
    {
        Assert.Null(RemovalKeys.Email(value));
        Assert.Null(RemovalKeys.Name(value));
    }

    [Fact]
    public void A_record_is_known_by_its_source_and_external_id()
    {
        var source = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("11111111-1111-1111-1111-111111111111:BLD-1", RemovalKeys.Record(source, "BLD-1"));
    }
}
