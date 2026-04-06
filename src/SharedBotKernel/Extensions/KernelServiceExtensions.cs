namespace SharedBotKernel.Extensions;

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedBotKernel.Constants;
using SharedBotKernel.Domain.Abstractions;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Time;
using SharedBotKernel.Options;

public static class KernelServiceExtensions
{
    public static IServiceCollection AddKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        var openAiSection = configuration.GetSection(OpenAiConstants.SectionName);
        var openAiOptions = new OpenAiOptions
        {
            BaseUrl = openAiSection[OpenAiConstants.BaseUrlKey] ?? OpenAiConstants.DefaultBaseUrl,
            Model = openAiSection[OpenAiConstants.ModelKey] ?? OpenAiConstants.DefaultModel,
            ApiKey = openAiSection[OpenAiConstants.ApiKeyKey],
            Temperature = ParseDouble(openAiSection[OpenAiConstants.TemperatureKey], OpenAiConstants.DefaultTemperature)
        };
        var envOpenAiKey = Environment.GetEnvironmentVariable(OpenAiConstants.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envOpenAiKey))
        {
            openAiOptions.ApiKey = envOpenAiKey;
        }

        var claudeSection = configuration.GetSection(ClaudeConstants.SectionName);
        var claudeOptions = new ClaudeOptions
        {
            BaseUrl = claudeSection[ClaudeConstants.BaseUrlKey] ?? ClaudeConstants.DefaultBaseUrl,
            Model = claudeSection[ClaudeConstants.ModelKey] ?? ClaudeConstants.DefaultModel,
            ApiKey = claudeSection[ClaudeConstants.ApiKeyKey],
            Temperature = ParseDouble(claudeSection[ClaudeConstants.TemperatureKey], ClaudeConstants.DefaultTemperature),
            MaxTokens = ParseInt(claudeSection[ClaudeConstants.MaxTokensKey], ClaudeConstants.DefaultMaxTokens)
        };
        var envClaudeKey = Environment.GetEnvironmentVariable(ClaudeConstants.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envClaudeKey))
        {
            claudeOptions.ApiKey = envClaudeKey;
        }

        var geminiSection = configuration.GetSection(GeminiConstants.SectionName);
        var geminiOptions = new GeminiOptions
        {
            BaseUrl = geminiSection[GeminiConstants.BaseUrlKey] ?? GeminiConstants.DefaultBaseUrl,
            Model = geminiSection[GeminiConstants.ModelKey] ?? GeminiConstants.DefaultModel,
            ApiKey = geminiSection[GeminiConstants.ApiKeyKey],
            Temperature = ParseDouble(geminiSection[GeminiConstants.TemperatureKey], GeminiConstants.DefaultTemperature),
            MaxTokens = ParseInt(geminiSection[GeminiConstants.MaxTokensKey], GeminiConstants.DefaultMaxTokens)
        };
        var envGeminiKey = Environment.GetEnvironmentVariable(GeminiConstants.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envGeminiKey))
        {
            geminiOptions.ApiKey = envGeminiKey;
        }

        var protectionSection = configuration.GetSection(AiCredentialProtectionConstants.SectionName);
        var aiCredentialProtectionOptions = new AiCredentialProtectionOptions
        {
            MasterKey = protectionSection[AiCredentialProtectionConstants.MasterKeyKey]
        };
        var envProtectionKey = Environment.GetEnvironmentVariable(AiCredentialProtectionConstants.MasterKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envProtectionKey))
        {
            aiCredentialProtectionOptions.MasterKey = envProtectionKey;
        }

        services.AddSingleton(openAiOptions);
        services.AddSingleton(claudeOptions);
        services.AddSingleton(geminiOptions);
        services.AddSingleton(aiCredentialProtectionOptions);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<OpenAiChatClient>();
        services.AddSingleton<ClaudeChatClient>();
        services.AddSingleton<GeminiChatClient>();
        services.AddSingleton<IAiSecretProtector, AiSecretProtector>();

        return services;
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
