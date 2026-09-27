using Npgsql;

namespace PermitTorch.Api.Data;

/// <summary>DATABASE_URL accepts either an Npgsql keyword connection string
/// (<c>Host=…;Database=…</c>) or the URL form Railway and most hosts emit
/// (<c>postgresql://user:pass@host:port/db?sslmode=require</c>); URLs are converted to the
/// keyword form Npgsql expects.</summary>
public static class DatabaseUrl
{
    public const string LocalDefault =
        "Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres";

    public static string ToNpgsqlConnectionString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return LocalDefault;
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            throw new FormatException("DATABASE_URL is not a valid postgres:// URL");

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (database.Length == 0)
            throw new FormatException("DATABASE_URL must include a database name");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = database,
        };

        var userInfo = uri.UserInfo;
        if (userInfo.Length > 0)
        {
            var separator = userInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? userInfo : userInfo[..separator]);
            if (separator >= 0) builder.Password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var key = Uri.UnescapeDataString(pair[..eq]);
            var val = Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(val.Replace("-", ""), ignoreCase: true, out var sslMode))
                builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }
}
