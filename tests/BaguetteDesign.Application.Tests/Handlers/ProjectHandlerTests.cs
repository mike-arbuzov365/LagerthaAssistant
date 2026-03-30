namespace BaguetteDesign.Application.Tests.Handlers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using Xunit;

public sealed class ProjectHandlerTests
{
    private const long ChatId = 1L;

    [Fact]
    public async Task ShowProjects_NoProjects_SendsEmptyMessage()
    {
        var (handler, sender, _, _) = Build([], []);

        await handler.ShowProjectsAsync(ChatId, "uk");

        Assert.Contains("немає активних проєктів", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowProjects_WithProjects_SendsListMessage()
    {
        var project = new Project { Id = 1, Title = "Logo", ClientUserId = "100", RevisionCount = 1, MaxRevisions = 3 };
        var (handler, sender, _, _) = Build([project], []);

        await handler.ShowProjectsAsync(ChatId, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Проєкти", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ShowProjectCard_ShowsAllFields()
    {
        var project = new Project
        {
            Id = 5, Title = "Brand Identity", ClientUserId = "200", ServiceType = "Branding",
            Budget = "$1000", RevisionCount = 2, MaxRevisions = 3, Status = ProjectStatus.Active
        };
        var (handler, sender, _, _) = Build([project], []);

        await handler.ShowProjectCardAsync(ChatId, 5, "uk");

        var text = sender.SentMessages[0].Text;
        Assert.Contains("Проєкт #5", text);
        Assert.Contains("Brand Identity", text);
        Assert.Contains("2/3", text);
    }

    [Fact]
    public async Task AddRevision_BelowLimit_IncrementsAndNotifiesClient()
    {
        var project = new Project { Id = 3, Title = "Logo", ClientUserId = "999", RevisionCount = 0, MaxRevisions = 3, Status = ProjectStatus.Active };
        var (handler, sender, _, _) = Build([project], []);

        await handler.AddRevisionAsync(ChatId, 3, "uk");

        Assert.Equal(1, project.RevisionCount);
        Assert.Equal(ProjectStatus.InRevision, project.Status);
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(999L, sender.SentMessages[0].ChatId);
    }

    [Fact]
    public async Task AddRevision_LimitReached_SendsAlert()
    {
        var project = new Project { Id = 4, Title = "Logo", ClientUserId = "888", RevisionCount = 2, MaxRevisions = 3 };
        var (handler, sender, _, _) = Build([project], []);

        await handler.AddRevisionAsync(ChatId, 4, "uk");

        Assert.Equal(3, project.RevisionCount);
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Contains("Ліміт правок досягнуто", sender.SentMessages[1].Text);
    }

    [Fact]
    public async Task ChangeProjectStatus_Completed_NotifiesClient()
    {
        var project = new Project { Id = 6, Title = "Logo", ClientUserId = "777", Status = ProjectStatus.Active };
        var (handler, sender, _, _) = Build([project], []);

        await handler.ChangeProjectStatusAsync(ChatId, 6, "completed", "uk");

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(777L, sender.SentMessages[0].ChatId);
        Assert.Contains("завершено", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task ConvertLeadToProject_CreatesProjectAndMarksConverted()
    {
        var lead = new Lead { Id = 10, UserId = "300", ServiceType = "Logo", Brand = "ACME", Status = LeadStatus.New };
        var (handler, sender, projectRepo, _) = Build([], [lead]);

        await handler.ConvertLeadToProjectAsync(ChatId, 10, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("конвертовано", sender.SentMessages[0].Text);
        Assert.Equal(LeadStatus.Converted, lead.Status);
        Assert.Single(projectRepo.Added);
        Assert.Equal("300", projectRepo.Added[0].ClientUserId);
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private static (ProjectHandler handler, FakeSender sender, FakeProjectRepository projectRepo, FakeLeadRepository leadRepo)
        Build(IEnumerable<Project> projects, IEnumerable<Lead> leads)
    {
        var sender = new FakeSender();
        var projectRepo = new FakeProjectRepository(projects);
        var leadRepo = new FakeLeadRepository(leads);
        var notifier = new FakeDesignerNotifier();
        var handler = new ProjectHandler(projectRepo, leadRepo, notifier, sender);
        return (handler, sender, projectRepo, leadRepo);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly List<Project> _projects;
        public List<Project> Added { get; } = [];
        public FakeProjectRepository(IEnumerable<Project> projects) => _projects = [.. projects];
        public Task AddAsync(Project project, CancellationToken ct = default) { Added.Add(project); _projects.Add(project); return Task.CompletedTask; }
        public Task<Project?> GetByIdAsync(int projectId, CancellationToken ct = default) => Task.FromResult(_projects.FirstOrDefault(p => p.Id == projectId));
        public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>([.. _projects]);
        public Task<IReadOnlyList<Project>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>(_projects.Where(p => p.ClientUserId == clientUserId).ToList());
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLeadRepository : ILeadRepository
    {
        private readonly List<Lead> _leads;
        public FakeLeadRepository(IEnumerable<Lead> leads) => _leads = [.. leads];
        public Task AddAsync(Lead lead, CancellationToken ct = default) { _leads.Add(lead); return Task.CompletedTask; }
        public Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default) => Task.FromResult(_leads.FirstOrDefault(l => l.Id == leadId));
        public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult(_leads.LastOrDefault(l => l.UserId == userId));
        public Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Lead>>([.. _leads]);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDesignerNotifier : IDesignerNotifier
    {
        public Task NotifyMessageReceivedAsync(long clientUserId, string message, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifySlotBookedAsync(long clientUserId, CalendarSlot slot, string? meetLink, CancellationToken ct = default) => Task.CompletedTask;
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
