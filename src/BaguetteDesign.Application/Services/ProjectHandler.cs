namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class ProjectHandler : IProjectHandler
{
    private readonly IProjectRepository _projects;
    private readonly ILeadRepository _leads;
    private readonly IDesignerNotifier _notifier;
    private readonly ITelegramBotSender _sender;

    public ProjectHandler(
        IProjectRepository projects,
        ILeadRepository leads,
        IDesignerNotifier notifier,
        ITelegramBotSender sender)
    {
        _projects = projects;
        _leads = leads;
        _notifier = notifier;
        _sender = sender;
    }

    public async Task ShowProjectsAsync(long chatId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var projects = await _projects.GetAllAsync(ct);

        if (projects.Count == 0)
        {
            var empty = locale == "uk"
                ? "📁 <b>Проєкти</b>\n\nПоки що немає активних проєктів."
                : "📁 <b>Projects</b>\n\nNo active projects yet.";
            await _sender.SendTextAsync(chatId, empty,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
            return;
        }

        var header = locale == "uk" ? "📁 <b>Проєкти</b>\n" : "📁 <b>Projects</b>\n";
        var buttons = projects.Take(10).Select(p =>
        {
            var icon = StatusIcon(p.Status);
            var rev = $"[{p.RevisionCount}/{p.MaxRevisions}]";
            var label = $"{icon} #{p.Id} {p.Title} {rev}";
            return new[] { Btn(label, $"project_card_{p.Id}") };
        }).ToArray<object[]>();

        await _sender.SendTextAsync(chatId, header,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: new { inline_keyboard = buttons }),
            cancellationToken: ct);
    }

    public async Task ShowProjectCardAsync(long chatId, int projectId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var project = await _projects.GetByIdAsync(projectId, ct);

        if (project is null)
        {
            var notFound = locale == "uk" ? "⚠️ Проєкт не знайдено." : "⚠️ Project not found.";
            await _sender.SendTextAsync(chatId, notFound, cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(locale == "uk" ? $"📁 <b>Проєкт #{project.Id}</b>" : $"📁 <b>Project #{project.Id}</b>");
        sb.AppendLine($"{StatusIcon(project.Status)} {StatusLabel(project.Status, locale)}");
        sb.AppendLine();
        sb.AppendLine($"📌 {project.Title}");
        if (!string.IsNullOrEmpty(project.ServiceType)) sb.AppendLine($"🎨 {project.ServiceType}");
        if (!string.IsNullOrEmpty(project.Budget)) sb.AppendLine($"💰 {project.Budget}");
        if (!string.IsNullOrEmpty(project.Deadline)) sb.AppendLine($"📅 {project.Deadline}");
        sb.AppendLine();

        var revIcon = project.IsRevisionLimitReached ? "🔴" : "🟢";
        sb.Append(revIcon);
        sb.AppendLine(locale == "uk"
            ? $" Правки: {project.RevisionCount}/{project.MaxRevisions}"
            : $" Revisions: {project.RevisionCount}/{project.MaxRevisions}");

        if (!string.IsNullOrEmpty(project.GoogleDriveFolderUrl))
            sb.AppendLine($"\n📂 <a href=\"{project.GoogleDriveFolderUrl}\">{(locale == "uk" ? "Google Drive папка" : "Google Drive folder")}</a>");

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    Btn(locale == "uk" ? "✏️ +Коло правок" : "✏️ +Revision", $"project_revision_{projectId}"),
                    Btn(locale == "uk" ? "💬 Діалог" : "💬 Dialog", $"inbox_open_{project.ClientUserId}")
                },
                new[]
                {
                    Btn("🔵 Active", $"project_status_{projectId}_active"),
                    Btn("⏳ Waiting", $"project_status_{projectId}_waiting"),
                    Btn("✏️ InRevision", $"project_status_{projectId}_inrevision")
                },
                new[]
                {
                    Btn("🟢 Completed", $"project_status_{projectId}_completed"),
                    Btn("⚪ Cancelled", $"project_status_{projectId}_cancelled")
                }
            }
        };

        await _sender.SendTextAsync(chatId, sb.ToString().TrimEnd(),
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard), cancellationToken: ct);
    }

    public async Task AddRevisionAsync(long chatId, int projectId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var project = await _projects.GetByIdAsync(projectId, ct);

        if (project is null)
        {
            await _sender.SendTextAsync(chatId,
                locale == "uk" ? "⚠️ Проєкт не знайдено." : "⚠️ Project not found.",
                cancellationToken: ct);
            return;
        }

        project.RevisionCount++;
        project.Status = ProjectStatus.InRevision;
        await _projects.SaveChangesAsync(ct);

        // Notify client about revision round
        if (long.TryParse(project.ClientUserId, out var clientChatId))
        {
            var remaining = project.MaxRevisions - project.RevisionCount;
            var clientMsg = locale == "uk"
                ? $"✏️ Розпочато коло правок #{project.RevisionCount}/{project.MaxRevisions}. Залишилось кіл: {remaining}."
                : $"✏️ Revision round #{project.RevisionCount}/{project.MaxRevisions} started. Rounds remaining: {remaining}.";
            await _sender.SendTextAsync(clientChatId, clientMsg, cancellationToken: ct);
        }

        // Alert designer if limit reached
        if (project.IsRevisionLimitReached)
        {
            var alert = locale == "uk"
                ? $"🔴 <b>Ліміт правок досягнуто!</b> Проєкт #{projectId}: {project.RevisionCount}/{project.MaxRevisions} кіл використано."
                : $"🔴 <b>Revision limit reached!</b> Project #{projectId}: {project.RevisionCount}/{project.MaxRevisions} rounds used.";
            await _sender.SendTextAsync(chatId, alert,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
            return;
        }

        var confirm = locale == "uk"
            ? $"✅ Коло правок #{project.RevisionCount} зараховано. Залишилось: {project.MaxRevisions - project.RevisionCount}."
            : $"✅ Revision round #{project.RevisionCount} added. Remaining: {project.MaxRevisions - project.RevisionCount}.";
        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task ChangeProjectStatusAsync(long chatId, int projectId, string newStatus, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null) return;

        project.Status = newStatus.ToLowerInvariant() switch
        {
            "active" => ProjectStatus.Active,
            "waiting" => ProjectStatus.WaitingMaterials,
            "inrevision" => ProjectStatus.InRevision,
            "completed" => ProjectStatus.Completed,
            "cancelled" => ProjectStatus.Cancelled,
            _ => project.Status
        };
        await _projects.SaveChangesAsync(ct);

        // Notify client on significant status changes
        if (project.Status is ProjectStatus.Completed or ProjectStatus.WaitingMaterials
            && long.TryParse(project.ClientUserId, out var clientChatId))
        {
            var clientMsg = project.Status == ProjectStatus.Completed
                ? (locale == "uk"
                    ? $"🎉 <b>Ваш проєкт «{project.Title}» завершено!</b> Дякуємо за співпрацю."
                    : $"🎉 <b>Your project «{project.Title}» is completed!</b> Thank you for working with us.")
                : (locale == "uk"
                    ? $"⏳ Для проєкту «{project.Title}» потрібні матеріали. Зв'яжіться з дизайнером."
                    : $"⏳ Project «{project.Title}» needs materials. Please contact the designer.");
            await _sender.SendTextAsync(clientChatId, clientMsg,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
        }

        var icon = StatusIcon(project.Status);
        var confirm = locale == "uk"
            ? $"{icon} Статус проєкту #{projectId} змінено на {StatusLabel(project.Status, locale)}."
            : $"{icon} Project #{projectId} status changed to {StatusLabel(project.Status, locale)}.";
        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task ConvertLeadToProjectAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var lead = await _leads.GetByIdAsync(leadId, ct);

        if (lead is null)
        {
            await _sender.SendTextAsync(chatId,
                locale == "uk" ? "⚠️ Лід не знайдено." : "⚠️ Lead not found.",
                cancellationToken: ct);
            return;
        }

        var project = Project.FromLead(lead);
        await _projects.AddAsync(project, ct);

        lead.Status = LeadStatus.Converted;
        await _leads.SaveChangesAsync(ct);
        await _projects.SaveChangesAsync(ct);

        var msg = locale == "uk"
            ? $"🟢 Лід #{leadId} конвертовано в проєкт. Назва: «{project.Title}»."
            : $"🟢 Lead #{leadId} converted to project: «{project.Title}».";
        await _sender.SendTextAsync(chatId, msg, cancellationToken: ct);
    }

    private static string StatusIcon(ProjectStatus status) => status switch
    {
        ProjectStatus.Active => "🔵",
        ProjectStatus.WaitingMaterials => "⏳",
        ProjectStatus.InRevision => "✏️",
        ProjectStatus.Completed => "🟢",
        ProjectStatus.Cancelled => "⚪",
        _ => "❓"
    };

    private static string StatusLabel(ProjectStatus status, string locale) => locale == "uk"
        ? status switch
        {
            ProjectStatus.Active => "Активний",
            ProjectStatus.WaitingMaterials => "Очікуємо матеріали",
            ProjectStatus.InRevision => "Правки",
            ProjectStatus.Completed => "Завершено",
            ProjectStatus.Cancelled => "Скасовано",
            _ => "?"
        }
        : status switch
        {
            ProjectStatus.Active => "Active",
            ProjectStatus.WaitingMaterials => "Waiting Materials",
            ProjectStatus.InRevision => "In Revision",
            ProjectStatus.Completed => "Completed",
            ProjectStatus.Cancelled => "Cancelled",
            _ => "?"
        };

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
