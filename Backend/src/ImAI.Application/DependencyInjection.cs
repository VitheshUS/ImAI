using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.ConversationSummaries;
namespace ImAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        // This staged feature requires an Infrastructure publisher only when invoked.
        // A factory keeps startup validation working until that implementation is added.
        services.Replace(ServiceDescriptor.Transient<IRequestHandler<RequestConversationSummary, bool>>(provider =>
            new RequestConversationSummaryHandler(provider.GetRequiredService<IConversationSummaryEventPublisher>())));
        return services;
    }
}
