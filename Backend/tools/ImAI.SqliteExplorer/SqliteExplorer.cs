using Microsoft.Data.Sqlite;

namespace ImAI.SqliteExplorer;

internal sealed class SqliteExplorer(string connectionString) : ISqliteExplorer
{
    public const int MaxRows = 500;

    public async Task<IReadOnlyList<SqliteTable>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, type FROM sqlite_schema WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var tables = new List<(string Name, string Type)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        var result = new List<SqliteTable>();
        foreach (var (name, type) in tables)
        {
            command.CommandText = "PRAGMA table_info(\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\")";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = new List<SqliteColumn>();
            while (await reader.ReadAsync(cancellationToken))
                columns.Add(new(reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetInt64(5) > 0));
            result.Add(new(name, type, columns));
        }
        return result;
    }

    public async Task<SqliteQueryResult> QueryAsync(string? sql, CancellationToken cancellationToken = default)
    {
        ReadOnlySql.Validate(sql);
        await using var connection = await OpenReadOnlyAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql!;
        command.CommandTimeout = 5;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count == MaxRows) return new(columns, rows, true);
            var row = new object?[reader.FieldCount];
            for (var index = 0; index < row.Length; index++)
                row[index] = reader.GetValue(index) switch
                {
                    DBNull => null,
                    byte[] bytes => $"[BLOB: {bytes.Length} bytes]",
                    var value => value
                };
            rows.Add(row);
        }
        return new(columns, rows, false);
    }

    private async Task<SqliteConnection> OpenReadOnlyAsync(CancellationToken cancellationToken)
    {
        var settings = new SqliteConnectionStringBuilder(connectionString)
        {
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        var connection = new SqliteConnection(settings.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA query_only = ON; PRAGMA trusted_schema = OFF;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
