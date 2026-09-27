using Npgsql;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Tests.Data;

public class DatabaseUrlTests
{
    [Fact]
    public void Keyword_connection_strings_pass_through_unchanged()
    {
        const string keyword = "Host=db;Port=6543;Database=pt;Username=u;Password=p";
        Assert.Equal(keyword, DatabaseUrl.ToNpgsqlConnectionString(keyword));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_value_falls_back_to_the_local_default(string? value) =>
        Assert.Equal(DatabaseUrl.LocalDefault, DatabaseUrl.ToNpgsqlConnectionString(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Development_falls_back_to_the_local_default(string? value) =>
        Assert.Equal(DatabaseUrl.LocalDefault, DatabaseUrl.Resolve(value, "Development"));

    [Theory]
    [InlineData(null, "Production")]
    [InlineData("", "Production")]
    [InlineData("  ", "Staging")]
    [InlineData(null, "Testing")]
    public void Missing_value_outside_development_fails_fast(string? value, string environment)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseUrl.Resolve(value, environment));
        Assert.Contains("DATABASE_URL is required outside Development", ex.Message);
    }

    [Fact]
    public void Resolve_converts_urls_in_every_environment()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseUrl.Resolve(
            "postgresql://u:p@postgres.railway.internal:5432/railway", "Production"));
        Assert.Equal("postgres.railway.internal", b.Host);
    }

    [Theory]
    [InlineData("postgresql://postgres:s3cret@postgres.railway.internal:5432/railway")]
    [InlineData("postgres://postgres:s3cret@postgres.railway.internal:5432/railway")]
    [InlineData("POSTGRESQL://postgres:s3cret@postgres.railway.internal:5432/railway")]
    public void Railway_urls_convert_to_keyword_form(string url)
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsqlConnectionString(url));
        Assert.Equal("postgres.railway.internal", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("railway", b.Database);
        Assert.Equal("postgres", b.Username);
        Assert.Equal("s3cret", b.Password);
    }

    [Fact]
    public void Url_encoded_credentials_are_decoded_and_ports_default_to_5432()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsqlConnectionString(
            "postgresql://app%40user:p%40ss%3Aw%2Fo%3Brd@example.proxy.rlwy.net/my%20db"));
        Assert.Equal("app@user", b.Username);
        Assert.Equal("p@ss:w/o;rd", b.Password);
        Assert.Equal(5432, b.Port);
        Assert.Equal("my db", b.Database);
    }

    [Fact]
    public void Non_default_ports_and_sslmode_are_kept()
    {
        var b = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsqlConnectionString(
            "postgresql://u:p@roundhouse.proxy.rlwy.net:41234/railway?sslmode=require"));
        Assert.Equal(41234, b.Port);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Theory]
    [InlineData("postgresql://u:p@host:5432/")]
    [InlineData("postgresql://")]
    public void Malformed_urls_fail_fast(string url) =>
        Assert.Throws<FormatException>(() => DatabaseUrl.ToNpgsqlConnectionString(url));
}
