namespace BaguetteDesign.Application.Tests.Handlers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using Xunit;

public sealed class StatusHandlerTests
{
    private const long ChatId = 11L;
    private const long UserId = 22L;

    [Fact]
    public async Task ShowStatus_NoLead_SendsNoBriefMessage()
    {
        var (handler, sender, _) = Build(lead: null);

        await handler.ShowStatusAsync(ChatId, UserId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("немає жодного запиту", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowStatus_NewLead_ShowsNewStatus()
    {
        var lead = new Lead { UserId = UserId.ToString(), Status = LeadStatus.New };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "uk");

        Assert.Contains("Новий", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowStatus_InProgress_ShowsInProgressStatus()
    {
        var lead = new Lead { UserId = UserId.ToString(), Status = LeadStatus.InProgress };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "uk");

        Assert.Contains("В роботі", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowStatus_WaitingMaterials_ListsMissingFields()
    {
        var lead = new Lead
        {
            UserId = UserId.ToString(),
            Status = LeadStatus.WaitingMaterials,
            ServiceType = "Logo",
        };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "uk");

        var text = sender.SentMessages[0].Text;
        Assert.Contains("Очікуємо матеріали", text);
        Assert.Contains("Бюджет", text);
        Assert.Contains("Дедлайн", text);
    }

    [Fact]
    public async Task ShowStatus_WaitingMaterials_AllFieldsFilled_ShowsContactHint()
    {
        var lead = new Lead
        {
            UserId = UserId.ToString(),
            Status = LeadStatus.WaitingMaterials,
            Brand = "ACME",
            Audience = "B2B",
            Style = "Minimalist",
            Deadline = "2 weeks",
            Budget = "$500"
        };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "uk");

        Assert.Contains("уточнення деталей", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowStatus_EnglishLocale_SendsEnglishText()
    {
        var lead = new Lead { UserId = UserId.ToString(), Status = LeadStatus.New };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "en");

        Assert.Contains("New", sender.SentMessages[0].Text);
        Assert.Contains("request received", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowStatus_WithAiSummary_IncludesSummaryInMessage()
    {
        var lead = new Lead
        {
            UserId = UserId.ToString(),
            Status = LeadStatus.InProgress,
            AiSummary = "Logo design for tech startup"
        };
        var (handler, sender, _) = Build(lead);

        await handler.ShowStatusAsync(ChatId, UserId, "en");

        Assert.Contains("Logo design for tech startup", sender.SentMessages[0].Text);
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private static (StatusHandler handler, FakeSender sender, FakeLeadRepository repo) Build(Lead? lead)
    {
        var sender = new FakeSender();
        var repo = new FakeLeadRepository(lead);
        var handler = new StatusHandler(repo, sender);
        return (handler, sender, repo);
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private readonly Lead? _lead;
        public FakeLeadRepository(Lead? lead) => _lead = lead;
        public Task AddAsync(Lead lead, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default) => Task.FromResult(_lead);
        public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult(_lead);
        public Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Lead>>(_lead is null ? [] : [_lead]);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
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
