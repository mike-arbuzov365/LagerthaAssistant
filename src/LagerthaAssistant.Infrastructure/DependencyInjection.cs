namespace LagerthaAssistant.Infrastructure;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Infrastructure.AI;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Repositories;
using LagerthaAssistant.Infrastructure.Time;

public static class DependencyInjection
{
    public static IServiceCollection AddLagerthaAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(OpenAiConstants.SectionName);

        var options = new OpenAiOptions
        {
            BaseUrl = section[OpenAiConstants.BaseUrlKey] ?? OpenAiConstants.DefaultBaseUrl,
            Model = section[OpenAiConstants.ModelKey] ?? OpenAiConstants.DefaultModel,
            ApiKey = section[OpenAiConstants.ApiKeyKey],
            Temperature = ParseDouble(section[OpenAiConstants.TemperatureKey], OpenAiConstants.DefaultTemperature)
        };

        var envApiKey = Environment.GetEnvironmentVariable(OpenAiConstants.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            options.ApiKey = envApiKey;
        }

        var connectionString = configuration.GetConnectionString(PersistenceConstants.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{PersistenceConstants.ConnectionStringName}' not found.");

        services.AddDbContext<AppDbContext>(db => db.UseSqlServer(connectionString));

        services.AddSingleton(options);
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton(sp =>
        {
            var currentOptions = sp.GetRequiredService<OpenAiOptions>();
            return new HttpClient
            {
                BaseAddress = new Uri(currentOptions.BaseUrl),
                Timeout = TimeSpan.FromSeconds(OpenAiConstants.HttpTimeoutSeconds)
            };
        });

        services.AddScoped<IAiChatClient, OpenAiChatClient>();
        services.AddScoped<IConversationSessionRepository, ConversationSessionRepository>();
        services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}

