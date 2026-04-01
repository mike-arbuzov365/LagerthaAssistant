namespace SharedBotKernel.Tests.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedBotKernel.Constants;
using SharedBotKernel.Domain.Abstractions;
using SharedBotKernel.Extensions;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Time;
using SharedBotKernel.Options;
using Xunit;

public sealed class KernelServiceExtensionsTests
{
    [Fact]
    public void AddKernelServices_ShouldRegisterCoreServices_WhenCalled()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{OpenAiConstants.SectionName}:{OpenAiConstants.ApiKeyKey}"] = "openai-key",
            [$"{ClaudeConstants.SectionName}:{ClaudeConstants.ApiKeyKey}"] = "claude-key",
            [$"{AiCredentialProtectionConstants.SectionName}:{AiCredentialProtectionConstants.MasterKeyKey}"] = "master-key"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKernelServices(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());
        Assert.NotNull(provider.GetRequiredService<OpenAiChatClient>());
        Assert.NotNull(provider.GetRequiredService<ClaudeChatClient>());
        Assert.IsType<AiSecretProtector>(provider.GetRequiredService<IAiSecretProtector>());
    }

    [Fact]
    public void AddKernelServices_ShouldPreferEnvironmentVariables_WhenBothConfigAndEnvironmentProvided()
    {
        using var _ = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            [OpenAiConstants.ApiKeyEnvironmentVariable] = "env-openai",
            [ClaudeConstants.ApiKeyEnvironmentVariable] = "env-claude",
            [AiCredentialProtectionConstants.MasterKeyEnvironmentVariable] = "env-master"
        });

        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{OpenAiConstants.SectionName}:{OpenAiConstants.ApiKeyKey}"] = "config-openai",
            [$"{ClaudeConstants.SectionName}:{ClaudeConstants.ApiKeyKey}"] = "config-claude",
            [$"{AiCredentialProtectionConstants.SectionName}:{AiCredentialProtectionConstants.MasterKeyKey}"] = "config-master"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKernelServices(configuration);

        using var provider = services.BuildServiceProvider();
        var openAi = provider.GetRequiredService<OpenAiOptions>();
        var claude = provider.GetRequiredService<ClaudeOptions>();
        var protection = provider.GetRequiredService<AiCredentialProtectionOptions>();

        Assert.Equal("env-openai", openAi.ApiKey);
        Assert.Equal("env-claude", claude.ApiKey);
        Assert.Equal("env-master", protection.MasterKey);
    }

    [Fact]
    public void AddKernelServices_ShouldUseDefaultNumbers_WhenConfigValuesInvalid()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{OpenAiConstants.SectionName}:{OpenAiConstants.TemperatureKey}"] = "NaN-temp",
            [$"{ClaudeConstants.SectionName}:{ClaudeConstants.TemperatureKey}"] = "bad-number",
            [$"{ClaudeConstants.SectionName}:{ClaudeConstants.MaxTokensKey}"] = "bad-int"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKernelServices(configuration);

        using var provider = services.BuildServiceProvider();
        var openAi = provider.GetRequiredService<OpenAiOptions>();
        var claude = provider.GetRequiredService<ClaudeOptions>();

        Assert.Equal(OpenAiConstants.DefaultTemperature, openAi.Temperature);
        Assert.Equal(ClaudeConstants.DefaultTemperature, claude.Temperature);
        Assert.Equal(ClaudeConstants.DefaultMaxTokens, claude.MaxTokens);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentVariableScope(Dictionary<string, string?> updatedValues)
        {
            foreach (var entry in updatedValues)
            {
                _originalValues[entry.Key] = Environment.GetEnvironmentVariable(entry.Key);
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }

        public void Dispose()
        {
            foreach (var entry in _originalValues)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}
