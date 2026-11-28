using Microsoft.Extensions.DependencyInjection;

namespace ImAI.SqliteExplorer.DependencyInjection;

public static class SqliteExplorerServiceCollectionExtensions
{
    public static IServiceCollection AddSqliteExplorer(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddScoped<ISqliteExplorer>(provider => new SqliteExplorer(connectionString(provider)));
        return services;
    }
}
