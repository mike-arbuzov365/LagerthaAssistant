namespace LagerthaAssistant.Infrastructure.Services;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class AiRuntimeSettingsService : IAiRuntimeSettingsService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ModelMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [AiProviderConstants.OpenAi] = ["gpt-4.1-mini", "gpt-4.1", "gpt-4o-mini"],
        [AiProviderConstants.Claude] = ["claude-3-5-haiku-latest", "claude-3-5-sonnet-latest", "claude-3-7-sonnet-latest"],
        [AiProviderConstants.Gemini] = ["gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro"]
    };

    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IAiCredentialRepository _credentialRepository;
    private readonly IAiSecretProtector _secretProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OpenAiOptions _openAiOptions;
    private readonly ClaudeOptions _claudeOptions;
    private readonly GeminiOptions _geminiOptions;
    private readonly ILogger<AiRuntimeSettingsService> _logger;

    public AiRuntimeSettingsService(
        IUserMemoryRepository userMemoryRepository,
        IAiCredentialRepository credentialRepository,
        IAiSecretProtector secretProtector,
        IUnitOfWork unitOfWork,
        OpenAiOptions openAiOptions,
        ClaudeOptions claudeOptions,
        GeminiOptions geminiOptions,
        ILogger<AiRuntimeSettingsService> logger)
    {
        _userMemoryRepository = userMemoryRepository;
        _credentialRepository = credentialRepository;
        _secretProtector = secretProtector;
        _unitOfWork = unitOfWork;
        _openAiOptions = openAiOptions;
        _claudeOptions = claudeOptions;
        _geminiOptions = geminiOptions;
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedProviders => AiProviderConstants.SupportedProviders;

    public bool TryNormalizeProvider(string? value, out string provider)
    {
        provider = AiProviderConstants.OpenAi;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "open ai")
        {
            normalized = AiProviderConstants.OpenAi;
        }
        else if (normalized is "anthropic")
        {
            normalized = AiProviderConstants.Claude;
        }
        else if (normalized is "google" or "google gemini")
        {
            normalized = AiProviderConstants.Gemini;
        }

        if (AiProviderConstants.SupportedProviders.Contains(normalized, StringComparer.Ordinal))
        {
            provider = normalized;
            return true;
        }

        return false;
    }

    public IReadOnlyList<string> GetSupportedModels(string provider)
    {
        var normalizedProvider = TryNormalizeProvider(provider, out var parsedProvider)
            ? parsedProvider
            : AiProviderConstants.OpenAi;

        var defaultModel = GetDefaultModel(normalizedProvider);
        if (!ModelMap.TryGetValue(normalizedProvider, out var models))
        {
            return [defaultModel];
        }

        return models
            .Append(defaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default)
    {
        var scoped = await _userMemoryRepository.GetByKeyAsync(
            UserPreferenceMemoryKeys.AiProvider,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        var value = scoped?.Value;
        if (string.IsNullOrWhiteSpace(value)
            && !scope.Channel.Equals(ConversationScope.DefaultChannel, StringComparison.Ordinal)
            && !scope.UserId.Equals(ConversationScope.DefaultUserId, StringComparison.Ordinal))
        {
            var legacy = await _userMemoryRepository.GetByKeyAsync(UserPreferenceMemoryKeys.AiProvider, cancellationToken);
            value = legacy?.Value;
        }

        return TryNormalizeProvider(value, out var provider)
            ? provider
            : AiProviderConstants.OpenAi;
    }

    public async Task<string> SetProviderAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeProvider(provider, out var normalizedProvider))
        {
            throw new InvalidOperationException("Unsupported AI provider.");
        }

        await UpsertPreferenceAsync(
            scope,
            UserPreferenceMemoryKeys.AiProvider,
            normalizedProvider,
            cancellationToken);

        return normalizedProvider;
    }

    public async Task<string> GetModelAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = TryNormalizeProvider(provider, out var parsedProvider)
            ? parsedProvider
            : AiProviderConstants.OpenAi;
        var preferenceKey = GetModelPreferenceKey(normalizedProvider);

        var scoped = await _userMemoryRepository.GetByKeyAsync(
            preferenceKey,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        var value = scoped?.Value;
        if (string.IsNullOrWhiteSpace(value)
            && !scope.Channel.Equals(ConversationScope.DefaultChannel, StringComparison.Ordinal)
            && !scope.UserId.Equals(ConversationScope.DefaultUserId, StringComparison.Ordinal))
        {
            var legacy = await _userMemoryRepository.GetByKeyAsync(preferenceKey, cancellationToken);
            value = legacy?.Value;
        }

        return string.IsNullOrWhiteSpace(value)
            ? GetDefaultModel(normalizedProvider)
            : value.Trim();
    }

    public async Task<string> SetModelAsync(
        ConversationScope scope,
        string provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeProvider(provider, out var normalizedProvider))
        {
            throw new InvalidOperationException("Unsupported AI provider.");
        }

        var normalizedModel = model?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            throw new InvalidOperationException("Model cannot be empty.");
        }

        await UpsertPreferenceAsync(
            scope,
            GetModelPreferenceKey(normalizedProvider),
            normalizedModel,
            cancellationToken);

        return normalizedModel;
    }

    public async Task<bool> HasStoredApiKeyAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeProvider(provider, out var normalizedProvider))
        {
            return false;
        }

        var credential = await _credentialRepository.GetAsync(
            scope.Channel,
            scope.UserId,
            normalizedProvider,
            cancellationToken);

        return credential is not null && !string.IsNullOrWhiteSpace(credential.EncryptedApiKey);
    }

    public async Task SetApiKeyAsync(
        ConversationScope scope,
        string provider,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeProvider(provider, out var normalizedProvider))
        {
            throw new InvalidOperationException("Unsupported AI provider.");
        }

        var normalizedApiKey = apiKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            throw new InvalidOperationException("API key cannot be empty.");
        }

        var encrypted = _secretProtector.Protect(normalizedApiKey);
        var now = DateTimeOffset.UtcNow;
        var existing = await _credentialRepository.GetAsync(
            scope.Channel,
            scope.UserId,
            normalizedProvider,
            cancellationToken);

        if (existing is null)
        {
            await _credentialRepository.AddAsync(new UserAiCredential
            {
                Channel = scope.Channel,
                UserId = scope.UserId,
                Provider = normalizedProvider,
                EncryptedApiKey = encrypted,
                UpdatedAtUtc = now
            }, cancellationToken);
        }
        else
        {
            existing.EncryptedApiKey = encrypted;
            existing.UpdatedAtUtc = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveApiKeyAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeProvider(provider, out var normalizedProvider))
        {
            return;
        }

        var existing = await _credentialRepository.GetAsync(
            scope.Channel,
            scope.UserId,
            normalizedProvider,
            cancellationToken);

        if (existing is null)
        {
            return;
        }

        await _credentialRepository.RemoveAsync(existing, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<AiRuntimeSettings> ResolveAsync(
        ConversationScope scope,
        CancellationToken cancellationToken = default)
    {
        var provider = await GetProviderAsync(scope, cancellationToken);
        var model = await GetModelAsync(scope, provider, cancellationToken);

        var stored = await _credentialRepository.GetAsync(scope.Channel, scope.UserId, provider, cancellationToken);
        if (stored is not null
            && !string.IsNullOrWhiteSpace(stored.EncryptedApiKey)
            && _secretProtector.TryUnprotect(stored.EncryptedApiKey, out var decrypted))
        {
            return new AiRuntimeSettings(provider, model, decrypted, AiApiKeySource.Stored);
        }

        if (stored is not null && !_secretProtector.TryUnprotect(stored.EncryptedApiKey, out _))
        {
            _logger.LogWarning(
                "Stored AI key cannot be decrypted. Channel={Channel}; UserId={UserId}; Provider={Provider}",
                scope.Channel,
                scope.UserId,
                provider);
        }

        var envKey = GetEnvironmentApiKey(provider);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return new AiRuntimeSettings(provider, model, envKey.Trim(), AiApiKeySource.Environment);
        }

        return new AiRuntimeSettings(provider, model, string.Empty, AiApiKeySource.Missing);
    }

    private async Task UpsertPreferenceAsync(
        ConversationScope scope,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        var existing = await _userMemoryRepository.GetByKeyAsync(
            key,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (existing is null)
        {
            await _userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = key,
                Value = value,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
                Channel = scope.Channel,
                UserId = scope.UserId
            }, cancellationToken);
        }
        else
        {
            existing.Value = value;
            existing.Confidence = 1.0;
            existing.IsActive = false;
            existing.LastSeenAtUtc = DateTimeOffset.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private string GetDefaultModel(string provider)
    {
        return provider switch
        {
            AiProviderConstants.Claude => _claudeOptions.Model,
            AiProviderConstants.Gemini => _geminiOptions.Model,
            _ => _openAiOptions.Model
        };
    }

    private static string GetModelPreferenceKey(string provider)
    {
        return provider switch
        {
            AiProviderConstants.Claude => UserPreferenceMemoryKeys.AiModelClaude,
            AiProviderConstants.Gemini => UserPreferenceMemoryKeys.AiModelGemini,
            _ => UserPreferenceMemoryKeys.AiModelOpenAi
        };
    }

    private string? GetEnvironmentApiKey(string provider)
    {
        return provider switch
        {
            AiProviderConstants.Claude => _claudeOptions.ApiKey,
            AiProviderConstants.Gemini => _geminiOptions.ApiKey,
            _ => _openAiOptions.ApiKey
        };
    }
}
