namespace BaguetteDesign.Application.Tests.Handlers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using Xunit;

public sealed class LeadHandlerTests
{
    private const long ChatId = 1L;

    [Fact]
    public async Task ShowLeads_NoLeads_SendsEmptyMessage()
    {
        var (handler, sender, _) = Build([]);

        await handler.ShowLeadsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("немає лідів", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowLeads_WithLeads_SendsListWithButtons()
    {
        var leads = new[]
        {
            new Lead { Id = 1, UserId = "100", ServiceType = "Logo", Status = LeadStatus.New },
            new Lead { Id = 2, UserId = "200", ServiceType = "Brand", Status = LeadStatus.InProgress }
        };
        var (handler, sender, _) = Build(leads);

        await handler.ShowLeadsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Ліди", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowLeadCard_ExistingLead_ShowsAllFields()
    {
        var lead = new Lead
        {
            Id = 5, UserId = "300", ServiceType = "Packaging", Brand = "ACME",
            Budget = "$500", Status = LeadStatus.WaitingMaterials, AiSummary = "Packaging design for ACME"
        };
        var (handler, sender, _) = Build([lead]);

        await handler.ShowLeadCardAsync(ChatId, 5, "uk");

        var text = sender.SentMessages[0].Text;
        Assert.Contains("Лід #5", text);
        Assert.Contains("ACME", text);
        Assert.Contains("$500", text);
        Assert.Contains("Packaging design for ACME", text);
    }

    [Fact]
    public async Task ShowLeadCard_NotFound_SendsWarning()
    {
        var (handler, sender, _) = Build([]);

        await handler.ShowLeadCardAsync(ChatId, 99, "uk");

        Assert.Contains("не знайдено", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ChangeLeadStatus_UpdatesAndConfirms()
    {
        var lead = new Lead { Id = 3, UserId = "400", Status = LeadStatus.New };
        var (handler, sender, repo) = Build([lead]);

        await handler.ChangeLeadStatusAsync(ChatId, 3, "inprogress", "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("В роботі", sender.SentMessages[0].Text);
        Assert.Equal(LeadStatus.InProgress, lead.Status);
    }

    [Fact]
    public async Task ShowLeadCard_EnglishLocale_ShowsEnglishLabels()
    {
        var lead = new Lead { Id = 7, UserId = "500", ServiceType = "Logo", Status = LeadStatus.New };
        var (handler, sender, _) = Build([lead]);

        await handler.ShowLeadCardAsync(ChatId, 7, "en");

        Assert.Contains("Lead #7", sender.SentMessages[0].Text);
        Assert.Contains("Service", sender.SentMessages[0].Text);
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private static (LeadHandler handler, FakeSender sender, FakeLeadRepository repo) Build(IEnumerable<Lead> leads)
    {
        var sender = new FakeSender();
        var repo = new FakeLeadRepository(leads);
        var service = new LeadService(repo);
        var handler = new LeadHandler(service, sender);
        return (handler, sender, repo);
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private readonly List<Lead> _leads;
        public FakeLeadRepository(IEnumerable<Lead> leads) => _leads = [.. leads];
        public Task AddAsync(Lead lead, CancellationToken ct = default) { _leads.Add(lead); return Task.CompletedTask; }
        public Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default) => Task.FromResult(_leads.FirstOrDefault(l => l.Id == leadId));
        public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult(_leads.LastOrDefault(l => l.UserId == userId));
        public Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken ct = default)
        {
            var result = status.HasValue ? _leads.Where(l => l.Status == status.Value).ToList() : _leads.ToList();
            return Task.FromResult<IReadOnlyList<Lead>>(result);
        }
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
