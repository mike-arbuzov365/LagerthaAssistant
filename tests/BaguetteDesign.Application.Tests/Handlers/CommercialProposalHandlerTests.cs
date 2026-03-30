namespace BaguetteDesign.Application.Tests.Handlers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using Xunit;

public sealed class CommercialProposalHandlerTests
{
    private const long ChatId = 1L;
    private const long DesignerUserId = 1L;

    [Fact]
    public async Task GenerateDraft_WithLead_SendsGeneratingAndDraft()
    {
        var lead = new Lead { Id = 5, UserId = "200", ServiceType = "Logo", Budget = "$300", Deadline = "2 weeks" };
        var (handler, sender, _, _) = Build([lead]);

        await handler.GenerateDraftAsync(ChatId, 5, "uk");

        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Contains("Генерую", sender.SentMessages[0].Text);
        Assert.Contains("Чернетка КП", sender.SentMessages[1].Text);
        Assert.Contains("AI draft", sender.SentMessages[1].Text);
    }

    [Fact]
    public async Task GenerateDraft_LeadNotFound_SendsWarning()
    {
        var (handler, sender, _, _) = Build([]);

        await handler.GenerateDraftAsync(ChatId, 99, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("не знайдено", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task SendProposal_WithDraft_ForwardsToClient()
    {
        var lead = new Lead { Id = 3, UserId = "777" };
        var (handler, sender, _, memory) = Build([lead]);
        await memory.SetAsync(ChatId.ToString(), "kp_draft_3", "KP text here", default);

        await handler.SendProposalAsync(ChatId, DesignerUserId, 3, "uk");

        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(777L, sender.SentMessages[0].ChatId);
        Assert.Equal("KP text here", sender.SentMessages[0].Text);
        Assert.Contains("надіслано", sender.SentMessages[1].Text);
    }

    [Fact]
    public async Task SendProposal_NoDraft_SendsWarning()
    {
        var (handler, sender, _, _) = Build([]);

        await handler.SendProposalAsync(ChatId, DesignerUserId, 5, "uk");

        Assert.Contains("не знайдена", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task DismissProposal_ClearsDraftAndConfirms()
    {
        var (handler, sender, _, memory) = Build([]);
        await memory.SetAsync(ChatId.ToString(), "kp_draft_5", "some kp", default);

        await handler.DismissProposalAsync(ChatId, DesignerUserId, 5, "uk");

        Assert.Contains("відхилено", sender.SentMessages[0].Text);
        Assert.Null(await memory.GetAsync(ChatId.ToString(), "kp_draft_5"));
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private static (CommercialProposalHandler handler, FakeSender sender, FakeLeadRepo leadRepo, FakeMemoryRepo memory)
        Build(IEnumerable<Lead> leads)
    {
        var sender = new FakeSender();
        var leadRepo = new FakeLeadRepo(leads);
        var memory = new FakeMemoryRepo();
        var ai = new FakeAiClient();
        var handler = new CommercialProposalHandler(leadRepo, memory, ai, sender);
        return (handler, sender, leadRepo, memory);
    }

    private sealed class FakeLeadRepo : ILeadRepository
    {
        private readonly List<Lead> _leads;
        public FakeLeadRepo(IEnumerable<Lead> leads) => _leads = [.. leads];
        public Task AddAsync(Lead lead, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default) => Task.FromResult(_leads.FirstOrDefault(l => l.Id == leadId));
        public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult<Lead?>(null);
        public Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Lead>>([.. _leads]);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMemoryRepo : IUserMemoryRepository
    {
        private readonly Dictionary<string, string> _store = [];
        public Task<string?> GetAsync(string userId, string key, CancellationToken ct = default) { _store.TryGetValue($"{userId}:{key}", out var val); return Task.FromResult(val); }
        public Task SetAsync(string userId, string key, string value, CancellationToken ct = default) { _store[$"{userId}:{key}"] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string userId, string key, CancellationToken ct = default) { _store.Remove($"{userId}:{key}"); return Task.CompletedTask; }
    }

    private sealed class FakeAiClient : IAiChatClient
    {
        public Task<AssistantCompletionResult> CompleteAsync(IReadOnlyCollection<ConversationMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("AI draft proposal", "claude", null));
    }

    private sealed class FakeSender : ITelegramBotSender
    {
        public List<(long ChatId, string Text)> SentMessages { get; } = [];
        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, TelegramSendOptions? options = null, int? messageThreadId = null, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((chatId, text));
            return Task.FromResult(new TelegramSendResult(true));
        }
        public Task<TelegramSendResult> AnswerCallbackQueryAsync(string id, string? text = null, CancellationToken ct = default) => Task.FromResult(new TelegramSendResult(true));
    }
}
