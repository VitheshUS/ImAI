namespace ImAI.SqliteExplorer;

internal static class ReadOnlySql
{
    public static void Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 20000)
            throw new ArgumentException("Enter a SQL query of up to 20,000 characters.", nameof(sql));

        var firstKeyword = string.Empty;
        var ended = false;
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (char.IsWhiteSpace(current)) continue;
            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                while (index + 1 < sql.Length && sql[index + 1] != '\n') index++;
                continue;
            }
            if (current == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                var end = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) throw new ArgumentException("SQL comment is not closed.", nameof(sql));
                index = end + 1;
                continue;
            }
            if (ended) throw new ArgumentException("Run one read-only statement at a time.", nameof(sql));
            if (firstKeyword.Length == 0)
            {
                var start = index;
                while (index < sql.Length && char.IsAsciiLetter(sql[index])) index++;
                firstKeyword = sql[start..index].ToUpperInvariant();
                if (firstKeyword is not ("SELECT" or "WITH" or "EXPLAIN"))
                    throw new ArgumentException("Only SELECT, WITH, and EXPLAIN queries are supported.", nameof(sql));
                index--;
                continue;
            }
            if (current == ';')
            {
                ended = true;
                continue;
            }
            if (current is '\'' or '"' or '`' or '[')
            {
                var closing = current == '[' ? ']' : current;
                while (++index < sql.Length)
                {
                    if (sql[index] != closing) continue;
                    if (closing != ']' && index + 1 < sql.Length && sql[index + 1] == closing)
                    {
                        index++;
                        continue;
                    }
                    break;
                }
                if (index == sql.Length) throw new ArgumentException("SQL quote is not closed.", nameof(sql));
            }
        }
        if (firstKeyword.Length == 0) throw new ArgumentException("Enter a SQL query.", nameof(sql));
    }
}
