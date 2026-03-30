namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class ContactHandlerTests
{
    private const long ChatId = 11L;
    private const long UserId = 22L;

    [Fact]
    public async Task ShowOptions_SendsThreeOptions()
    {
        var (handler, sender, _, _, _, _) = Build();

        await handler.ShowOptionsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Зв'язатися", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task PromptForMessage_SetsAwaitingFlag()
    {
        var (handler, sender, _, _, memory, _) = Build();

        await handler.PromptForMessageAsync(ChatId, "uk");

        Assert.True(await handler.IsAwaitingMessageAsync(ChatId.ToString()));
        Assert.Single(sender.SentMessages);
        Assert.Contains("Напишіть", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleSendMessage_ForwardsToDesignerAndClearsFlag()
    {
        var (handler, sender, notifier, _, memory, _) = Build();

        await handler.PromptForMessageAsync(ChatId, "uk");
        await handler.HandleSendMessageAsync(ChatId, UserId, "Привіт!", "uk");

        Assert.Equal(UserId, notifier.LastNotifiedUserId);
        Assert.Equal("Привіт!", notifier.LastMessage);
        Assert.False(await handler.IsAwaitingMessageAsync(ChatId.ToString()));
        // 1 prompt + 1 confirmation
        Assert.Equal(2, sender.SentMessages.Count);
    }

    [Fact]
    public async Task ShowCalendarSlots_WhenSlotsAvailable_SendsKeyboard()
    {
        var slots = new List<CalendarSlot>
        {
            new(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1)),
            new(DateTimeOffset.UtcNow.AddDays(1).AddHours(2), DateTimeOffset.UtcNow.AddDays(1).AddHours(3))
        };
        var (handler, sender, _, calendar, _, _) = Build(slots);

        await handler.ShowCalendarSlotsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Оберіть зручний час", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowCalendarSlots_WhenNoSlots_SendsNoSlotsMessage()
    {
        var (handler, sender, _, _, _, _) = Build([]);

        await handler.ShowCalendarSlotsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("вільних слотів", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleSendMessage_EnglishLocale_SendsEnglishReply()
    {
        var (handler, sender, _, _, _, _) = Build();

        await handler.HandleSendMessageAsync(ChatId, UserId, "Hello!", "en");

        Assert.Contains("Your message has been sent", sender.SentMessages[0].Text);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private static (
        ContactHandler handler,
        FakeSender sender,
        FakeDesignerNotifier notifier,
        FakeCalendarService calendar,
        FakeUserMemoryRepository memory,
        FakeRepositories repos
    ) Build(IReadOnlyList<CalendarSlot>? slots = null)
    {
        var sender = new FakeSender();
        var notifier = new FakeDesignerNotifier();
        var calendar = new FakeCalendarService(slots ?? [
            new(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1))
        ]);
        var memory = new FakeUserMemoryRepository();
        var repos = new FakeRepositories();

        var handler = new ContactHandler(
            calendar, repos, repos, notifier, memory, sender);

        return (handler, sender, notifier, calendar, memory, repos);
    }

    private sealed class FakeCalendarService : ICalendarService
    {
        private readonly IReadOnlyList<CalendarSlot> _slots;
        public FakeCalendarService(IReadOnlyList<CalendarSlot> slots) => _slots = slots;

        public Task<IReadOnlyList<CalendarSlot>> GetAvailableSlotsAsync(int daysAhead = 7, CancellationToken ct = default)
            => Task.FromResult(_slots);

        public Task<string?> BookSlotAsync(CalendarSlot slot, string userId, string summary, CancellationToken ct = default)
            => Task.FromResult<string?>("https://meet.google.com/fake-link");
    }

    private sealed class FakeDesignerNotifier : IDesignerNotifier
    {
        public long LastNotifiedUserId { get; private set; }
        public string? LastMessage { get; private set; }

        public Task NotifyMessageReceivedAsync(long clientUserId, string message, CancellationToken ct = default)
        {
            LastNotifiedUserId = clientUserId;
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task NotifySlotBookedAsync(long clientUserId, CalendarSlot slot, string? meetLink, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        private readonly Dictionary<string, string> _store = [];

        public Task<string?> GetAsync(string userId, string key, CancellationToken ct = default)
        {
            _store.TryGetValue($"{userId}:{key}", out var val);
            return Task.FromResult(val);
        }

        public Task SetAsync(string userId, string key, string value, CancellationToken ct = default)
        {
            _store[$"{userId}:{key}"] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string userId, string key, CancellationToken ct = default)
        {
            _store.Remove($"{userId}:{key}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRepositories : ICalendarEventRepository, INotificationRepository
    {
        public Task AddAsync(CalendarEvent e, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddAsync(Notification n, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset asOf, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Notification>>([]);
        public Task MarkSentAsync(int notificationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
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
            string id, string? text = null, CancellationToken ct = default)
            => Task.FromResult(new TelegramSendResult(true));
    }
}
