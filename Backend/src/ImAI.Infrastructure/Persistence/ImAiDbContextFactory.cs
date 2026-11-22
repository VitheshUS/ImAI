using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ImAI.Infrastructure.Persistence;

public sealed class ImAiDbContextFactory : IDesignTimeDbContextFactory<ImAiDbContext>
{
    public ImAiDbContext CreateDbContext(string[] args)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImAI.slnx")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Cannot locate the Backend directory containing ImAI.slnx.");

        var databasePath = Path.Combine(directory.FullName, "imai.db");
        var options = new DbContextOptionsBuilder<ImAiDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new ImAiDbContext(options);
    }
}
