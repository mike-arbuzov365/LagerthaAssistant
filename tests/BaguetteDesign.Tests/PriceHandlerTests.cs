namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class PriceHandlerTests
{
    private const long ChatId = 42L;

    [Fact]
    public async Task ShowCategories_WhenCategoriesExist_SendsMessageWithKeyboard()
    {
        var sender = new FakeSender();
        var priceService = new FakePriceService(["Logo", "Branding"], []);
        var handler = new PriceHandler(priceService, sender);

        await handler.ShowCategoriesAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Оберіть категорію", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategories_WhenEmpty_SendsNoDataMessage()
    {
        var sender = new FakeSender();
        var priceService = new FakePriceService([], []);
        var handler = new PriceHandler(priceService, sender);

        await handler.ShowCategoriesAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("недоступний", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategoryItems_WhenItemsExist_ShowsFormattedList()
    {
        var items = new List<PriceItem>
        {
            new() { Name = "Logo Design", PriceAmount = 300, Currency = "USD", Category = "Logo" },
            new() { Name = "Brand Kit", PriceAmount = 800, Currency = "USD", Category = "Logo" }
        };
        var sender = new FakeSender();
        var priceService = new FakePriceService(["Logo"], items);
        var handler = new PriceHandler(priceService, sender);

        await handler.ShowCategoryItemsAsync(ChatId, "Logo", "en");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Logo Design", sender.SentMessages[0].Text);
        Assert.Contains("300", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategoryItems_WhenEmpty_SendsNoDataMessage()
    {
        var sender = new FakeSender();
        var priceService = new FakePriceService([], []);
        var handler = new PriceHandler(priceService, sender);

        await handler.ShowCategoryItemsAsync(ChatId, "Logo", "en");

        Assert.Single(sender.SentMessages);
        Assert.Contains("No items", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategories_EnglishLocale_SendsEnglishText()
    {
        var sender = new FakeSender();
        var priceService = new FakePriceService(["Logo"], []);
        var handler = new PriceHandler(priceService, sender);

        await handler.ShowCategoriesAsync(ChatId, "en");

        Assert.Contains("Select a service", sender.SentMessages[0].Text);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakePriceService : IPriceService
    {
        private readonly IReadOnlyList<string> _categories;
        private readonly IReadOnlyList<PriceItem> _items;

        public FakePriceService(IReadOnlyList<string> categories, IReadOnlyList<PriceItem> items)
        {
            _categories = categories;
            _items = items;
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
            => Task.FromResult(_categories);

        public Task<IReadOnlyList<PriceItem>> GetByCategoryAsync(string category, CancellationToken ct = default)
            => Task.FromResult(_items);
    }

    private sealed class FakeSender : ITelegramBotSender
    {
        public List<(long ChatId, string Text)> SentMessages { get; } = [];

        public Task<TelegramSendResult> SendTextAsync(
            long chatId, string text,
            TelegramSendOptions? options = null,
            int? messageThreadId = null,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add((chatId, text));
            return Task.FromResult(new TelegramSendResult(true));
        }

        public Task<TelegramSendResult> AnswerCallbackQueryAsync(
            string callbackQueryId, string? text = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new TelegramSendResult(true));
    }
}
