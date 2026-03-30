namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class PortfolioHandlerTests
{
    private const long ChatId = 99L;

    [Fact]
    public async Task ShowCategories_WhenCategoriesExist_SendsKeyboard()
    {
        var sender = new FakeSender();
        var service = new FakePortfolioService(["Branding", "Logo"], []);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoriesAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Оберіть категорію", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategories_WhenEmpty_SendsNoDataMessage()
    {
        var sender = new FakeSender();
        var service = new FakePortfolioService([], []);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoriesAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("недоступне", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategoryItems_WhenCasesExist_SendsOneMsgPerCasePlusNav()
    {
        var cases = new List<PortfolioCase>
        {
            new() { Title = "Nike Rebrand", Category = "Branding", Description = "Full rebrand." },
            new() { Title = "Startup Logo", Category = "Branding" }
        };
        var sender = new FakeSender();
        var service = new FakePortfolioService(["Branding"], cases);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoryItemsAsync(ChatId, "Branding", "en");

        // 2 case messages + 1 navigation message
        Assert.Equal(3, sender.SentMessages.Count);
        Assert.Contains("Nike Rebrand", sender.SentMessages[0].Text);
        Assert.Contains("Startup Logo", sender.SentMessages[1].Text);
        Assert.Contains("What would you like", sender.SentMessages[2].Text);
    }

    [Fact]
    public async Task ShowCategoryItems_WhenEmpty_SendsNoDataMessage()
    {
        var sender = new FakeSender();
        var service = new FakePortfolioService([], []);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoryItemsAsync(ChatId, "Branding", "en");

        Assert.Single(sender.SentMessages);
        Assert.Contains("No cases", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategories_UkLocale_SendsUkText()
    {
        var sender = new FakeSender();
        var service = new FakePortfolioService(["Logo"], []);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoriesAsync(ChatId, "uk");

        Assert.Contains("Оберіть категорію", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCategoryItems_CaseWithDescription_IncludesDescription()
    {
        var cases = new List<PortfolioCase>
        {
            new() { Title = "My Case", Category = "Logo", Description = "A cool logo for startup." }
        };
        var sender = new FakeSender();
        var service = new FakePortfolioService(["Logo"], cases);
        var handler = new PortfolioHandler(service, sender);

        await handler.ShowCategoryItemsAsync(ChatId, "Logo", "en");

        Assert.Contains("A cool logo for startup.", sender.SentMessages[0].Text);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakePortfolioService : IPortfolioService
    {
        private readonly IReadOnlyList<string> _categories;
        private readonly IReadOnlyList<PortfolioCase> _cases;

        public FakePortfolioService(IReadOnlyList<string> categories, IReadOnlyList<PortfolioCase> cases)
        {
            _categories = categories;
            _cases = cases;
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
            => Task.FromResult(_categories);

        public Task<IReadOnlyList<PortfolioCase>> GetByCategoryAsync(string category, CancellationToken ct = default)
            => Task.FromResult(_cases);
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
