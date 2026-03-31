// Shared kernel types — entities and base classes now live in SharedBotKernel
global using SharedBotKernel.Domain.Base;
global using SharedBotKernel.Domain.Entities;
global using MessageRole = SharedBotKernel.Domain.AI.MessageRole;

// Telegram types → SharedBotKernel (duplicates removed)
global using ITelegramBotSender = SharedBotKernel.Abstractions.ITelegramBotSender;
global using TelegramSendResult = SharedBotKernel.Abstractions.TelegramSendResult;
global using TelegramSendOptions = SharedBotKernel.Abstractions.TelegramSendOptions;
global using TelegramOptions = SharedBotKernel.Options.TelegramOptions;

// AI types → SharedBotKernel (duplicates removed)
global using IAiChatClient = SharedBotKernel.Abstractions.IAiChatClient;
global using IAiRuntimeSettingsService = SharedBotKernel.Infrastructure.AI.IAiRuntimeSettingsService;
global using IAiSecretProtector = SharedBotKernel.Infrastructure.AI.IAiSecretProtector;
global using IConversationScopeAccessor = SharedBotKernel.Infrastructure.AI.IConversationScopeAccessor;
global using AssistantCompletionResult = SharedBotKernel.Models.AI.AssistantCompletionResult;
global using AssistantUsage = SharedBotKernel.Models.AI.AssistantUsage;
global using AiRuntimeSettings = SharedBotKernel.Models.AI.AiRuntimeSettings;
global using AiApiKeySource = SharedBotKernel.Models.AI.AiApiKeySource;
global using ConversationScope = SharedBotKernel.Models.Agents.ConversationScope;
global using OpenAiOptions = SharedBotKernel.Options.OpenAiOptions;
global using OpenAiConstants = SharedBotKernel.Constants.OpenAiConstants;
global using ConversationMessage = SharedBotKernel.Domain.AI.ConversationMessage;
