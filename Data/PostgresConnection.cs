using Npgsql;

namespace guithu.Data;

/// <summary>Chuẩn hoá URL PostgreSQL của các nhà cung cấp cloud cho Npgsql.</summary>
public static class PostgresConnection
{
    public static string Normalize(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (!uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)))
            return connectionString;

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(Uri.UnescapeDataString(parts[1]), true, out var sslMode))
                builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }
}
