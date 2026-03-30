namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class StartCommandHandlerTests
{
    private const long AdminId = 12345L;
    private const long ClientId = 99999L;

    [Fact]
    public async Task HandleAsync_Designer_SendsDesignerMenu()
    {
        var fakeSender = new FakeTelegramSender();
        var handler = new StartCommandHandler(new RoleRouter(AdminId), fakeSender);

        await handler.HandleAsync(chatId: AdminId, userId: AdminId, languageCode: "uk");

        Assert.Single(fakeSender.SentMessages);
        Assert.Contains("Твоя робоча панель", fakeSender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleAsync_Client_SendsClientMenu()
    {
        var fakeSender = new FakeTelegramSender();
        var handler = new StartCommandHandler(new RoleRouter(AdminId), fakeSender);

        await handler.HandleAsync(chatId: ClientId, userId: ClientId, languageCode: "uk");

        Assert.Single(fakeSender.SentMessages);
        Assert.Contains("допоможу вам замовити дизайн", fakeSender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleAsync_EnglishLocale_SendsEnglishText()
    {
        var fakeSender = new FakeTelegramSender();
        var handler = new StartCommandHandler(new RoleRouter(AdminId), fakeSender);

        await handler.HandleAsync(chatId: ClientId, userId: ClientId, languageCode: "en");

        Assert.Contains("Welcome", fakeSender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleAsync_NullLocale_DefaultsToEnglish()
    {
        var fakeSender = new FakeTelegramSender();
        var handler = new StartCommandHandler(new RoleRouter(AdminId), fakeSender);

        await handler.HandleAsync(chatId: ClientId, userId: ClientId, languageCode: null);

        Assert.Contains("Welcome", fakeSender.SentMessages[0].Text);
    }

    private sealed class FakeTelegramSender : ITelegramBotSender
    {
        public List<(long ChatId, string Text)> SentMessages { get; } = [];

        public Task<TelegramSendResult> SendTextAsync(
            long chatId,
            string text,
            TelegramSendOptions? options = null,
            int? messageThreadId = null,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add((chatId, text));
            return Task.FromResult(new TelegramSendResult(true));
        }

        public Task<TelegramSendResult> AnswerCallbackQueryAsync(
            string callbackQueryId,
            string? text = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TelegramSendResult(true));
    }
}
