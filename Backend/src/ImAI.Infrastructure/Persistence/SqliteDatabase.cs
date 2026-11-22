namespace ImAI.Infrastructure.Persistence;

internal static class SqliteDatabase
{
    public static string DefaultConnectionString
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImAI.slnx")))
                directory = directory.Parent;
            var databasePath = Path.Combine(directory?.FullName ?? AppContext.BaseDirectory, "imai.db");
            return new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        }
    }
}
