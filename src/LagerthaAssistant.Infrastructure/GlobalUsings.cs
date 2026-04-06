// Shared kernel types — entities and base classes now live in SharedBotKernel
global using SharedBotKernel.Domain.Base;
global using SharedBotKernel.Domain.Entities;
global using MessageRole = SharedBotKernel.Domain.AI.MessageRole;

// AI infrastructure classes → SharedBotKernel (duplicates removed)
global using ClaudeChatClient = SharedBotKernel.Infrastructure.AI.ClaudeChatClient;
global using OpenAiChatClient = SharedBotKernel.Infrastructure.AI.OpenAiChatClient;
global using ResolvingAiChatClient = SharedBotKernel.Infrastructure.AI.ResolvingAiChatClient;

// AI interfaces → SharedBotKernel (duplicates removed)
global using IAiChatClient = SharedBotKernel.Abstractions.IAiChatClient;
global using IAiSecretProtector = SharedBotKernel.Infrastructure.AI.IAiSecretProtector;
global using IAiRuntimeSettingsService = SharedBotKernel.Infrastructure.AI.IAiRuntimeSettingsService;
global using IConversationScopeAccessor = SharedBotKernel.Infrastructure.AI.IConversationScopeAccessor;

// AI models → SharedBotKernel (duplicates removed)
global using AssistantCompletionResult = SharedBotKernel.Models.AI.AssistantCompletionResult;
global using AssistantUsage = SharedBotKernel.Models.AI.AssistantUsage;
global using AiRuntimeSettings = SharedBotKernel.Models.AI.AiRuntimeSettings;
global using AiApiKeySource = SharedBotKernel.Models.AI.AiApiKeySource;
global using ConversationScope = SharedBotKernel.Models.Agents.ConversationScope;
global using ConversationMessage = SharedBotKernel.Domain.AI.ConversationMessage;

// AI options/constants → SharedBotKernel (duplicates removed)
global using ClaudeOptions = SharedBotKernel.Options.ClaudeOptions;
global using OpenAiOptions = SharedBotKernel.Options.OpenAiOptions;
global using GeminiOptions = SharedBotKernel.Options.GeminiOptions;
global using AiCredentialProtectionOptions = SharedBotKernel.Options.AiCredentialProtectionOptions;
global using ClaudeConstants = SharedBotKernel.Constants.ClaudeConstants;
global using OpenAiConstants = SharedBotKernel.Constants.OpenAiConstants;
global using GeminiConstants = SharedBotKernel.Constants.GeminiConstants;
global using AiCredentialProtectionConstants = SharedBotKernel.Constants.AiCredentialProtectionConstants;
global using AiProviderConstants = SharedBotKernel.Constants.AiProviderConstants;
