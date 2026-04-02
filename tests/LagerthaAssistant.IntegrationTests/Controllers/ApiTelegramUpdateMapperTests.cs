namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api;
using LagerthaAssistant.Api.Contracts;
using Xunit;

public sealed class ApiTelegramUpdateMapperTests
{
    [Fact]
    public void TryMapInbound_WithWebAppDataOnly_ShouldMap()
    {
        var update = new TelegramWebhookUpdateRequest(
            UpdateId: 1,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(2002, false, "en", "mike", "Mike", null),
                Chat: new TelegramChatInfo(1001, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: null,
                WebAppData: new TelegramIncomingWebAppData(
                    Data: "{\"type\":\"settings_saved\",\"locale\":\"en\"}",
                    ButtonText: "⚙️ Settings")),
            EditedMessage: null,
            CallbackQuery: null);

        var mapped = ApiTelegramUpdateMapper.TryMapInbound(update, out var inbound);

        Assert.True(mapped);
        Assert.False(inbound.IsCallback);
        Assert.Equal("2002", inbound.UserId);
        Assert.Equal("{\"type\":\"settings_saved\",\"locale\":\"en\"}", inbound.WebAppData);
        Assert.Equal(string.Empty, inbound.Text);
    }

    [Fact]
    public void TryMapInbound_WithoutTextMediaAndWebAppData_ShouldReturnFalse()
    {
        var update = new TelegramWebhookUpdateRequest(
            UpdateId: 1,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(2002, false, "en", "mike", "Mike", null),
                Chat: new TelegramChatInfo(1001, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: null),
            EditedMessage: null,
            CallbackQuery: null);

        var mapped = ApiTelegramUpdateMapper.TryMapInbound(update, out _);

        Assert.False(mapped);
    }
}
