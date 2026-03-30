namespace BaguetteDesign.Tests;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Application.Services;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class FileHandlerTests
{
    private const long ChatId = 1L;
    private const long UserId = 100L;

    [Fact]
    public async Task HandleIncomingFile_PdfFile_ClassifiesAsText()
    {
        var (handler, sender, repo, _) = Build();

        await handler.HandleIncomingFileAsync(ChatId, UserId, "tg_file_1", "brief.pdf", "application/pdf", 10240, "uk");

        Assert.Single(sender.SentMessages);
        Assert.Contains("Текстовий матеріал", sender.SentMessages[0].Text);
        Assert.Single(repo.Added);
        Assert.Equal("text", repo.Added[0].FileType);
    }

    [Fact]
    public async Task HandleIncomingFile_PngFile_ClassifiesAsReference()
    {
        var (handler, sender, repo, _) = Build();

        await handler.HandleIncomingFileAsync(ChatId, UserId, "tg_file_2", "logo.png", "image/png", 5120, "uk");

        Assert.Contains("Референс", sender.SentMessages[0].Text);
        Assert.Equal("reference", repo.Added[0].FileType);
    }

    [Fact]
    public async Task HandleIncomingFile_WithActiveProject_AttachesToProject()
    {
        var project = new Project { Id = 7, ClientUserId = UserId.ToString(), Status = ProjectStatus.Active };
        var (handler, sender, repo, _) = Build(projects: [project]);

        await handler.HandleIncomingFileAsync(ChatId, UserId, "tg_file_3", "doc.docx", null, 2048, "uk");

        Assert.Equal(7, repo.Added[0].ProjectId);
        Assert.Contains("проєкту #7", sender.SentMessages[0].Text);
    }

    [Fact]
    public async Task RequestMaterials_SendsToClientAndConfirmsToDesigner()
    {
        var (handler, sender, _, _) = Build();

        await handler.RequestMaterialsAsync(ChatId, UserId.ToString(), "uk");

        Assert.Equal(2, sender.SentMessages.Count);
        Assert.Equal(UserId, sender.SentMessages[0].ChatId);
        Assert.Contains("запитує матеріали", sender.SentMessages[0].Text);
        Assert.Contains("надіслано клієнту", sender.SentMessages[1].Text);
    }

    [Fact]
    public async Task HandleIncomingFile_EnglishLocale_SendsEnglishConfirmation()
    {
        var (handler, sender, _, _) = Build();

        await handler.HandleIncomingFileAsync(ChatId, UserId, "tg_file_4", "ref.jpg", "image/jpeg", 1024, "en");

        Assert.Contains("Reference image", sender.SentMessages[0].Text);
        Assert.Contains("received", sender.SentMessages[0].Text);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private static (FileHandler handler, FakeSender sender, FakeFileRepo repo, FakeProjectRepo projectRepo)
        Build(IEnumerable<Project>? projects = null)
    {
        var sender = new FakeSender();
        var repo = new FakeFileRepo();
        var projectRepo = new FakeProjectRepo(projects ?? []);
        var handler = new FileHandler(repo, projectRepo, sender);
        return (handler, sender, repo, projectRepo);
    }

    private sealed class FakeFileRepo : IClientFileRepository
    {
        public List<ClientFile> Added { get; } = [];
        public Task AddAsync(ClientFile file, CancellationToken ct = default) { Added.Add(file); return Task.CompletedTask; }
        public Task<IReadOnlyList<ClientFile>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ClientFile>>([]);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProjectRepo : IProjectRepository
    {
        private readonly List<Project> _projects;
        public FakeProjectRepo(IEnumerable<Project> projects) => _projects = [.. projects];
        public Task AddAsync(Project project, CancellationToken ct = default) { _projects.Add(project); return Task.CompletedTask; }
        public Task<Project?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_projects.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>([.. _projects]);
        public Task<IReadOnlyList<Project>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Project>>(_projects.Where(p => p.ClientUserId == clientUserId).ToList());
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
