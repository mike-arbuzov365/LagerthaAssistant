namespace LagerthaAssistant.Application.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Memory;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Application.Services.Memory;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, AssistantSessionOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IAssistantSessionService, AssistantSessionService>();
        services.AddSingleton<IConversationMemoryExtractor, ConversationMemoryExtractor>();

        return services;
    }
}

