namespace BaguetteDesign.Application.Tests.Handlers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using Xunit;

public sealed class QuestionHandlerTests
{
    private const long ChatId = 42L;
    private const long UserId = 99L;

    [Fact]
    public async Task HandleAsync_SendsAiReplyToUser()
    {
        var sender = new FakeTelegramSender();
        var aiClient = new FakeAiChatClient("Звичайно, ось відповідь!");
        var repo = new FakeConversationRepository();
        var handler = new QuestionHandler(aiClient, repo, sender);

        await handler.HandleAsync(ChatId, UserId, "Яка ціна?", "uk");

        Assert.Single(sender.SentMessages);
        Assert.Equal(ChatId, sender.SentMessages[0].ChatId);
        Assert.Equal("Звичайно, ось відповідь!", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task HandleAsync_SavesUserAndAssistantEntries()
    {
        var sender = new FakeTelegramSender();
        var aiClient = new FakeAiChatClient("OK");
        var repo = new FakeConversationRepository();
        var handler = new QuestionHandler(aiClient, repo, sender);

        await handler.HandleAsync(ChatId, UserId, "Привіт!", "uk");

        Assert.Equal(2, repo.SavedEntries.Count);
        Assert.Equal(MessageRole.User, repo.SavedEntries[0].Role);
        Assert.Equal("Привіт!", repo.SavedEntries[0].Content);
        Assert.Equal(MessageRole.Assistant, repo.SavedEntries[1].Role);
        Assert.Equal("OK", repo.SavedEntries[1].Content);
    }

    [Fact]
    public async Task HandleAsync_PassesHistoryToAiClient()
    {
        var sender = new FakeTelegramSender();
        var aiClient = new FakeAiChatClient("response");
        var repo = new FakeConversationRepository();
        repo.PreloadHistory(new ConversationHistoryEntry
        {
            Role = MessageRole.User,
            Content = "Попереднє питання",
            SentAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var handler = new QuestionHandler(aiClient, repo, sender);

        await handler.HandleAsync(ChatId, UserId, "Нове питання", "uk");

        // system + 1 history + 1 new = 3 messages sent to AI
        Assert.Equal(3, aiClient.ReceivedMessages.Count);
    }

    [Fact]
    public async Task HandleAsync_FindsOrCreatesSession_ByUserId()
    {
        var sender = new FakeTelegramSender();
        var aiClient = new FakeAiChatClient("ok");
        var repo = new FakeConversationRepository();
        var handler = new QuestionHandler(aiClient, repo, sender);

        await handler.HandleAsync(ChatId, UserId, "test", "en");

        Assert.Equal(UserId.ToString(), repo.CreatedUserId);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeAiChatClient : IAiChatClient
    {
        private readonly string _reply;
        public List<ConversationMessage> ReceivedMessages { get; } = [];
        public FakeAiChatClient(string reply) => _reply = reply;

        public Task<AssistantCompletionResult> CompleteAsync(
            IReadOnlyCollection<ConversationMessage> messages,
            CancellationToken cancellationToken = default)
        {
            ReceivedMessages.AddRange(messages);
            return Task.FromResult(new AssistantCompletionResult(_reply, "fake-model", null));
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<ConversationHistoryEntry> SavedEntries { get; } = [];
        public string? CreatedUserId { get; private set; }
        private readonly List<ConversationHistoryEntry> _history = [];
        private readonly ConversationSession _session = ConversationSession.Create(Guid.NewGuid(), channel: "telegram", userId: "99");

        public void PreloadHistory(ConversationHistoryEntry entry) => _history.Add(entry);

        public Task<ConversationSession> FindOrCreateSessionAsync(string userId, CancellationToken ct = default)
        {
            CreatedUserId = userId;
            return Task.FromResult(_session);
        }

        public Task<ConversationSession?> FindSessionAsync(string userId, CancellationToken ct = default)
            => Task.FromResult<ConversationSession?>(_session);

        public Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentHistoryAsync(int sessionId, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConversationHistoryEntry>>(_history);

        public Task<IReadOnlyList<ConversationSession>> GetAllClientSessionsAsync(string excludeUserId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConversationSession>>([]);

        public Task AddEntryAsync(ConversationHistoryEntry entry, CancellationToken ct = default)
        {
            SavedEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeTelegramSender : ITelegramBotSender
    {
        public List<(long ChatId, string Text)> SentMessages { get; } = [];

        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, TelegramSendOptions? options = null, int? messageThreadId = null, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((chatId, text));
            return Task.FromResult(new TelegramSendResult(true));
        }

        public Task<TelegramSendResult> AnswerCallbackQueryAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TelegramSendResult(true));
    }
}
