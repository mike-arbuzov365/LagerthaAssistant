namespace LagerthaAssistant.Application.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Memory;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Application.Services.Memory;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, AssistantSessionOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IAssistantSessionService, AssistantSessionService>();
        services.AddSingleton<IConversationMemoryExtractor, ConversationMemoryExtractor>();
        services.AddSingleton<IVocabularyReplyParser, VocabularyReplyParser>();
        services.AddScoped<IVocabularyIndexService, VocabularyIndexService>();
        services.AddScoped<IVocabularyWorkflowService, VocabularyWorkflowService>();
        services.AddScoped<IVocabularyPersistenceService, VocabularyPersistenceService>();
        services.AddScoped<IVocabularySyncProcessor, VocabularySyncProcessor>();

        services.AddSingleton<IConversationIntentRouter, ConversationIntentRouter>();
        services.AddScoped<IConversationOrchestrator, ConversationOrchestrator>();
        services.AddScoped<IConversationMetricsService, ConversationMetricsService>();
        services.AddScoped<IConversationAgent, CommandConversationAgent>();
        services.AddScoped<IConversationAgent, VocabularyConversationAgent>();

        return services;
    }
}

