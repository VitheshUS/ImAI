namespace ImAI.SqliteExplorer;

/// <summary>Temporary development-only access to the application's SQLite database.</summary>
public interface ISqliteExplorer
{
    Task<IReadOnlyList<SqliteTable>> GetTablesAsync(CancellationToken cancellationToken = default);
    Task<SqliteQueryResult> QueryAsync(string? sql, CancellationToken cancellationToken = default);
}

public sealed record SqliteColumn(string Name, string Type, bool NotNull, bool PrimaryKey);
public sealed record SqliteTable(string Name, string Type, IReadOnlyList<SqliteColumn> Columns);
public sealed record SqliteQueryResult(IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, bool Truncated);
