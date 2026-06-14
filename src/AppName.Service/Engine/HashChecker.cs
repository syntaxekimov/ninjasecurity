using Microsoft.Data.Sqlite;

namespace NinjaSecurity.Service.Engine;

public class HashChecker
{
    private readonly string _dbPath;

    public HashChecker(string? dbPath = null)
    {
        _dbPath = dbPath ?? AppPaths.MalSearcherDbPath;
        EnsureTable();
    }

    public async Task<HashCheckResult> IsKnownThreatAsync(string sha256, CancellationToken ct = default)
    {
        if (!File.Exists(_dbPath))
            return new HashCheckResult(false, null);

        var normalized = sha256.ToLowerInvariant();
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT threat_name FROM hashes WHERE sha256 = @hash LIMIT 1";
        cmd.Parameters.AddWithValue("@hash", normalized);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string name
            ? new HashCheckResult(true, name)
            : new HashCheckResult(false, null);
    }

    // Only for testing — adds a hash directly to the local DB
    public async Task AddForTestingAsync(string sha256, string threatName)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO hashes (sha256, threat_name) VALUES (@h, @n)";
        cmd.Parameters.AddWithValue("@h", sha256.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@n", threatName);
        await cmd.ExecuteNonQueryAsync();
    }

    private void EnsureTable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS hashes (
                sha256 TEXT PRIMARY KEY,
                threat_name TEXT NOT NULL
            )
        """;
        cmd.ExecuteNonQuery();
    }
}

public record HashCheckResult(bool IsKnown, string? ThreatName);
