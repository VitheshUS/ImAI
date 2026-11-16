using ImAI.Application.Abstractions;
using ImAI.Infrastructure.Chat;
using ImAI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
namespace ImAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IChatResponder, DummyChatResponder>();
        services.AddScoped<IDocumentStorage, StubDocumentStorage>();
        return services;
    }
}
