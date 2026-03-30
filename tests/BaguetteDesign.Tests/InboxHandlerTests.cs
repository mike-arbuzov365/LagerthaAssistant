namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Domain.Entities;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Telegram;
using SharedBotKernel.Models.AI;
using Xunit;

public sealed class InboxHandlerTests
{
    private const long DesignerChatId = 1L;
    private const long DesignerUserId = 1L;
    private const string ClientUserId = "999";

    [Fact]
    public async Task ShowDialogs_NoSessions_SendsEmptyMessage()
    {
        var (handler, sender, _, _, _, _, _) = Build();

        await handler.ShowDialogsAsync(DesignerChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("немає звернень", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowDialogs_WithSessions_SendsDialogList()
    {
        var session = new ConversationSession { UserId = ClientUserId };
        var (handler, sender, convRepo, _, _, _, _) = Build(sessions: [session]);

        await handler.ShowDialogsAsync(DesignerChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Inbox", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task OpenDialog_SendsHistoryAndDraft()
    {
        var session = new ConversationSession { UserId = ClientUserId };
        var (handler, sender, convRepo, _, _, _, _) = Build(sessions: [session]);

        await handler.OpenDialogAsync(DesignerChatId, DesignerUserId, ClientUserId, "uk");

        Assert.Single(sender.SentMessages);
        var text = sender.SentMessages[0].Text;
        Assert.Contains("Клієнт", text);
        Assert.Contains("Чернетка", text);
    }

    [Fact]
    public async Task SendDraft_ForwardsDraftToClient()
    {
        var (handler, sender, _, _, memory, _, _) = Build();
        await memory.SetAsync(DesignerUserId.ToString(), $"inbox_draft_{ClientUserId}", "Hello client!", default);

        await handler.SendDraftAsync(DesignerChatId, DesignerUserId, ClientUserId, "uk");

        // message to client + confirmation to designer
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(long.Parse(ClientUserId), sender.SentMessages[0].ChatId);
        Assert.Contains("Hello client!", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task SendDraft_WhenNoDraft_SendsWarning()
    {
        var (handler, sender, _, _, _, _, _) = Build();

        await handler.SendDraftAsync(DesignerChatId, DesignerUserId, ClientUserId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Чернетка не знайдена", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task DismissDraft_ClearsDraftAndConfirms()
    {
        var (handler, sender, _, _, memory, _, _) = Build();
        await memory.SetAsync(DesignerUserId.ToString(), $"inbox_draft_{ClientUserId}", "some draft", default);

        await handler.DismissDraftAsync(DesignerChatId, DesignerUserId, ClientUserId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("відхилено", sender.SentMessages[0].Text);
        var remaining = await memory.GetAsync(DesignerUserId.ToString(), $"inbox_draft_{ClientUserId}");
        Assert.Null(remaining);
    }

    [Fact]
    public async Task SetManualMode_SetsFlag()
    {
        var (handler, sender, _, _, _, _, _) = Build();

        await handler.SetManualModeAsync(DesignerChatId, DesignerUserId, ClientUserId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Ручний режим", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task IsDesignerInManualMode_WhenFlagSet_ReturnsTrue()
    {
        var (handler, _, _, _, memory, _, _) = Build();
        await memory.SetAsync(DesignerUserId.ToString(), "inbox_active_client", ClientUserId, default);
        await memory.SetAsync(DesignerUserId.ToString(), $"inbox_manual_{ClientUserId}", "1", default);

        var result = await handler.IsDesignerInManualModeAsync(DesignerUserId);

        Assert.True(result);
    }

    [Fact]
    public async Task HandleDesignerManualMessage_ForwardsToClient()
    {
        var (handler, sender, _, _, memory, _, _) = Build();
        await memory.SetAsync(DesignerUserId.ToString(), "inbox_active_client", ClientUserId, default);

        await handler.HandleDesignerManualMessageAsync(DesignerChatId, DesignerUserId, "Hey there!", "uk");

        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(long.Parse(ClientUserId), sender.SentMessages[0].ChatId);
        Assert.Equal("Hey there!", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ChangeDialogStatus_UpdatesAndConfirms()
    {
        var (handler, sender, _, dialogStates, _, _, _) = Build();

        await handler.ChangeDialogStatusAsync(DesignerChatId, ClientUserId, "inprogress", "uk");

        Assert.Single(sender.SentMessages);
        var state = await dialogStates.GetByClientUserIdAsync(ClientUserId);
        Assert.NotNull(state);
        Assert.Equal(DialogStatus.InProgress, state.Status);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private static (
        InboxHandler handler,
        FakeSender sender,
        FakeConversationRepository convRepo,
        FakeDialogStateRepository dialogStates,
        FakeUserMemoryRepository memory,
        FakeLeadRepository leads,
        FakeAiClient ai
    ) Build(IReadOnlyList<ConversationSession>? sessions = null)
    {
        var sender = new FakeSender();
        var convRepo = new FakeConversationRepository(sessions ?? []);
        var dialogStates = new FakeDialogStateRepository();
        var memory = new FakeUserMemoryRepository();
        var leads = new FakeLeadRepository();
        var ai = new FakeAiClient();
        var roleRouter = new FakeRoleRouter();

        var handler = new InboxHandler(convRepo, dialogStates, leads, memory, ai, sender, roleRouter);
        return (handler, sender, convRepo, dialogStates, memory, leads, ai);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly List<ConversationSession> _sessions;
        public FakeConversationRepository(IReadOnlyList<ConversationSession> sessions)
            => _sessions = [.. sessions];

        public Task<ConversationSession> FindOrCreateSessionAsync(string userId, CancellationToken ct = default)
        {
            var s = _sessions.FirstOrDefault(x => x.UserId == userId)
                ?? new ConversationSession { UserId = userId };
            return Task.FromResult(s);
        }

        public Task<ConversationSession?> FindSessionAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(_sessions.FirstOrDefault(x => x.UserId == userId));

        public Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentHistoryAsync(int sessionId, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConversationHistoryEntry>>([]);

        public Task<IReadOnlyList<ConversationSession>> GetAllClientSessionsAsync(string excludeUserId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ConversationSession>>(
                _sessions.Where(s => s.UserId != excludeUserId).ToList());

        public Task AddEntryAsync(ConversationHistoryEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDialogStateRepository : IDialogStateRepository
    {
        private readonly Dictionary<string, DialogState> _store = [];

        public Task<IReadOnlyList<DialogState>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DialogState>>([.. _store.Values]);

        public Task<DialogState?> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default)
        {
            _store.TryGetValue(clientUserId, out var val);
            return Task.FromResult(val);
        }

        public Task UpsertAsync(DialogState state, CancellationToken ct = default)
        {
            _store[state.ClientUserId] = state;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class FakeLeadRepository : ILeadRepository
    {
        public Task AddAsync(Lead lead, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult<Lead?>(null);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAiClient : IAiChatClient
    {
        public Task<AssistantCompletionResult> CompleteAsync(
            IReadOnlyCollection<ConversationMessage> messages,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("AI draft reply", "claude", null));
    }

    private sealed class FakeRoleRouter : IRoleRouter
    {
        public UserRole Resolve(long userId) => userId == DesignerUserId
            ? UserRole.Designer
            : UserRole.Client;
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
