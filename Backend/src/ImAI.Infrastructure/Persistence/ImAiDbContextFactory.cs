using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ImAI.Infrastructure.Persistence;

public sealed class ImAiDbContextFactory : IDesignTimeDbContextFactory<ImAiDbContext>
{
    public ImAiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ImAI")
            ?? SqliteDatabase.DefaultConnectionString;
        var options = new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connectionString).Options;
        return new ImAiDbContext(options);
    }
}
