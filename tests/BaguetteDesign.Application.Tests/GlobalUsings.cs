global using SharedBotKernel.Domain.Base;
global using SharedBotKernel.Domain.Entities;
global using MessageRole = SharedBotKernel.Domain.AI.MessageRole;
global using ConversationMessage = SharedBotKernel.Domain.AI.ConversationMessage;
global using AssistantCompletionResult = SharedBotKernel.Models.AI.AssistantCompletionResult;
global using IAiChatClient = SharedBotKernel.Infrastructure.AI.IAiChatClient;
global using ITelegramBotSender = SharedBotKernel.Infrastructure.Telegram.ITelegramBotSender;
global using TelegramSendResult = SharedBotKernel.Infrastructure.Telegram.TelegramSendResult;
global using TelegramSendOptions = SharedBotKernel.Infrastructure.Telegram.TelegramSendOptions;
