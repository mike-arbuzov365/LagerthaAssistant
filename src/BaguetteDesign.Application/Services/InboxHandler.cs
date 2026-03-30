namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Domain.Entities;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class InboxHandler : IInboxHandler
{
    private const string ActiveClientKey = "inbox_active_client";
    private const string ManualModePrefix = "inbox_manual_";
    private const string DraftPrefix = "inbox_draft_";

    private readonly IConversationRepository _conversations;
    private readonly IDialogStateRepository _dialogStates;
    private readonly ILeadRepository _leads;
    private readonly IUserMemoryRepository _memory;
    private readonly IAiChatClient _ai;
    private readonly ITelegramBotSender _sender;
    private readonly IRoleRouter _roleRouter;

    public InboxHandler(
        IConversationRepository conversations,
        IDialogStateRepository dialogStates,
        ILeadRepository leads,
        IUserMemoryRepository memory,
        IAiChatClient ai,
        ITelegramBotSender sender,
        IRoleRouter roleRouter)
    {
        _conversations = conversations;
        _dialogStates = dialogStates;
        _leads = leads;
        _memory = memory;
        _ai = ai;
        _sender = sender;
        _roleRouter = roleRouter;
    }

    public async Task ShowDialogsAsync(long chatId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var designerUserId = chatId.ToString();

        // Load all client sessions and join with dialog states
        var sessions = await _conversations.GetAllClientSessionsAsync(designerUserId, ct);
        var states = (await _dialogStates.GetAllAsync(ct))
            .ToDictionary(s => s.ClientUserId);

        if (sessions.Count == 0)
        {
            var empty = locale == "uk"
                ? "📩 <b>Inbox</b>\n\nПоки що немає звернень від клієнтів."
                : "📩 <b>Inbox</b>\n\nNo client messages yet.";
            await _sender.SendTextAsync(chatId, empty,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(locale == "uk" ? "📩 <b>Inbox</b>\n" : "📩 <b>Inbox</b>\n");

        var buttons = new List<object[]>();

        foreach (var session in sessions.Take(10))
        {
            states.TryGetValue(session.UserId, out var state);
            var statusIcon = StatusIcon(state?.Status ?? DialogStatus.New);
            var preview = state?.LastClientMessagePreview ?? "...";
            if (preview.Length > 40) preview = preview[..40] + "…";

            var label = $"{statusIcon} {session.UserId} — {preview}";
            buttons.Add([Btn(label, $"inbox_open_{session.UserId}")]);
        }

        var keyboard = new { inline_keyboard = buttons.ToArray() };

        await _sender.SendTextAsync(chatId, sb.ToString().TrimEnd(),
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard), cancellationToken: ct);
    }

    public async Task OpenDialogAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);

        // Save active client for the designer
        await _memory.SetAsync(designerUserId.ToString(), ActiveClientKey, clientUserId, ct);

        // Load conversation history
        var session = await _conversations.FindSessionAsync(clientUserId, ct);
        var historyLines = new System.Text.StringBuilder();

        if (session is not null)
        {
            var history = await _conversations.GetRecentHistoryAsync(session.Id, 10, ct);
            foreach (var entry in history)
            {
                var who = entry.Role == MessageRole.User
                    ? (locale == "uk" ? "👤 Клієнт" : "👤 Client")
                    : (locale == "uk" ? "🤖 Бот" : "🤖 Bot");
                historyLines.AppendLine($"{who}: {entry.Content}");
            }
        }

        // Load brief / lead
        var lead = await _leads.GetLatestByUserIdAsync(clientUserId, ct);
        var briefLines = new System.Text.StringBuilder();
        if (lead is not null)
        {
            briefLines.AppendLine(locale == "uk" ? "\n📋 <b>Бриф:</b>" : "\n📋 <b>Brief:</b>");
            if (!string.IsNullOrEmpty(lead.ServiceType)) briefLines.AppendLine($"• Послуга: {lead.ServiceType}");
            if (!string.IsNullOrEmpty(lead.Brand)) briefLines.AppendLine($"• Бренд: {lead.Brand}");
            if (!string.IsNullOrEmpty(lead.Budget)) briefLines.AppendLine($"• Бюджет: {lead.Budget}");
            if (!string.IsNullOrEmpty(lead.Deadline)) briefLines.AppendLine($"• Дедлайн: {lead.Deadline}");
            if (!string.IsNullOrEmpty(lead.AiSummary)) briefLines.AppendLine($"\n💬 {lead.AiSummary}");
        }

        var header = locale == "uk"
            ? $"👤 <b>Клієнт {clientUserId}</b>"
            : $"👤 <b>Client {clientUserId}</b>";

        var historySection = historyLines.Length > 0
            ? $"\n\n{(locale == "uk" ? "💬 <b>Остання переписка:</b>" : "💬 <b>Recent chat:</b>")}\n{historyLines.ToString().TrimEnd()}"
            : string.Empty;

        var fullText = $"{header}{historySection}{briefLines}";

        // Generate AI draft reply
        var draftText = await GenerateDraftAsync(clientUserId, lead, locale, ct);
        await _memory.SetAsync(designerUserId.ToString(), $"{DraftPrefix}{clientUserId}", draftText, ct);

        var isManual = await IsManualModeForClientAsync(designerUserId.ToString(), clientUserId, ct);
        var modeLabel = isManual
            ? (locale == "uk" ? "🟢 Ручний режим" : "🟢 Manual mode")
            : (locale == "uk" ? "🤖 Авто режим" : "🤖 Auto mode");

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    Btn(locale == "uk" ? "✅ Надіслати чернетку" : "✅ Send draft", $"inbox_send_{clientUserId}"),
                    Btn(locale == "uk" ? "❌ Відхилити" : "❌ Dismiss", $"inbox_dismiss_{clientUserId}")
                },
                new[]
                {
                    Btn(modeLabel, isManual ? $"inbox_auto_{clientUserId}" : $"inbox_manual_{clientUserId}")
                },
                new[]
                {
                    Btn("🟡 New", $"inbox_status_{clientUserId}_new"),
                    Btn("🔵 InProgress", $"inbox_status_{clientUserId}_inprogress"),
                    Btn("⏳ Waiting", $"inbox_status_{clientUserId}_waiting"),
                    Btn("⚪ Closed", $"inbox_status_{clientUserId}_closed")
                }
            }
        };

        var draftSection = locale == "uk"
            ? $"\n\n✏️ <b>Чернетка відповіді:</b>\n{draftText}"
            : $"\n\n✏️ <b>Draft reply:</b>\n{draftText}";

        await _sender.SendTextAsync(chatId, fullText + draftSection,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard), cancellationToken: ct);
    }

    public async Task SendDraftAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var draft = await _memory.GetAsync(designerUserId.ToString(), $"{DraftPrefix}{clientUserId}", ct);

        if (string.IsNullOrWhiteSpace(draft))
        {
            var noDraft = locale == "uk" ? "⚠️ Чернетка не знайдена." : "⚠️ No draft found.";
            await _sender.SendTextAsync(chatId, noDraft, cancellationToken: ct);
            return;
        }

        // Send to client
        if (long.TryParse(clientUserId, out var clientChatId))
            await _sender.SendTextAsync(clientChatId, draft, cancellationToken: ct);

        // Save as assistant message in client's conversation
        await SaveAssistantMessageAsync(clientUserId, draft, ct);

        // Update dialog status to InProgress
        await UpdateDialogStatusAsync(clientUserId, DialogStatus.InProgress, ct);

        // Clear draft
        await _memory.DeleteAsync(designerUserId.ToString(), $"{DraftPrefix}{clientUserId}", ct);

        var confirm = locale == "uk"
            ? $"✅ Повідомлення надіслано клієнту {clientUserId}."
            : $"✅ Message sent to client {clientUserId}.";
        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task DismissDraftAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        await _memory.DeleteAsync(designerUserId.ToString(), $"{DraftPrefix}{clientUserId}", ct);

        var confirm = locale == "uk"
            ? "🗑️ Чернетку відхилено. Напишіть вручну або відкрийте діалог знову."
            : "🗑️ Draft dismissed. Write manually or reopen the dialog.";
        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task SetManualModeAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        await _memory.SetAsync(designerUserId.ToString(), $"{ManualModePrefix}{clientUserId}", "1", ct);

        var msg = locale == "uk"
            ? $"✏️ Ручний режим для клієнта {clientUserId}. Напишіть повідомлення — бот перешле його клієнту."
            : $"✏️ Manual mode for client {clientUserId}. Type a message — the bot will forward it.";
        await _sender.SendTextAsync(chatId, msg, cancellationToken: ct);
    }

    public async Task ChangeDialogStatusAsync(long chatId, string clientUserId, string newStatus, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var status = newStatus.ToLowerInvariant() switch
        {
            "new" => DialogStatus.New,
            "inprogress" => DialogStatus.InProgress,
            "waiting" => DialogStatus.Waiting,
            "closed" => DialogStatus.Closed,
            _ => DialogStatus.New
        };

        await UpdateDialogStatusAsync(clientUserId, status, ct);

        var icon = StatusIcon(status);
        var msg = locale == "uk"
            ? $"{icon} Статус клієнта {clientUserId} змінено."
            : $"{icon} Status for client {clientUserId} updated.";
        await _sender.SendTextAsync(chatId, msg, cancellationToken: ct);
    }

    public async Task<bool> IsDesignerInManualModeAsync(long designerUserId, CancellationToken ct = default)
    {
        var activeClient = await _memory.GetAsync(designerUserId.ToString(), ActiveClientKey, ct);
        if (string.IsNullOrEmpty(activeClient)) return false;

        return await IsManualModeForClientAsync(designerUserId.ToString(), activeClient, ct);
    }

    public async Task HandleDesignerManualMessageAsync(long designerChatId, long designerUserId, string text, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var activeClient = await _memory.GetAsync(designerUserId.ToString(), ActiveClientKey, ct);

        if (string.IsNullOrEmpty(activeClient))
        {
            var noClient = locale == "uk"
                ? "⚠️ Жоден клієнт не вибраний. Відкрийте діалог через /inbox."
                : "⚠️ No client selected. Open a dialog via /inbox.";
            await _sender.SendTextAsync(designerChatId, noClient, cancellationToken: ct);
            return;
        }

        // Forward to client
        if (long.TryParse(activeClient, out var clientChatId))
            await _sender.SendTextAsync(clientChatId, text, cancellationToken: ct);

        // Save as assistant message in client's conversation
        await SaveAssistantMessageAsync(activeClient, text, ct);

        var confirm = locale == "uk"
            ? $"✅ Пересланo клієнту {activeClient}."
            : $"✅ Forwarded to client {activeClient}.";
        await _sender.SendTextAsync(designerChatId, confirm, cancellationToken: ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> GenerateDraftAsync(string clientUserId, Lead? lead, string locale, CancellationToken ct)
    {
        var session = await _conversations.FindSessionAsync(clientUserId, ct);
        var recentHistory = session is not null
            ? await _conversations.GetRecentHistoryAsync(session.Id, 5, ct)
            : (IReadOnlyList<ConversationHistoryEntry>)[];

        var systemText = locale == "uk"
            ? "Ти асистент дизайнера. Склади коротку, ввічливу відповідь клієнту українською мовою (2-3 речення)."
            : "You are a designer's assistant. Write a short, polite reply to the client in English (2-3 sentences).";

        var briefContext = lead is not null
            ? $"\nBrief: service={lead.ServiceType}, budget={lead.Budget}, deadline={lead.Deadline}"
            : string.Empty;

        var messages = new List<ConversationMessage>
        {
            ConversationMessage.Create(MessageRole.System, systemText, DateTimeOffset.UtcNow)
        };

        foreach (var entry in recentHistory)
        {
            var role = entry.Role == MessageRole.User ? MessageRole.User : MessageRole.Assistant;
            messages.Add(ConversationMessage.Create(role, entry.Content, entry.SentAtUtc));
        }

        var userPrompt = $"Generate a reply to the client.{briefContext}";
        messages.Add(ConversationMessage.Create(MessageRole.User, userPrompt, DateTimeOffset.UtcNow));

        try
        {
            var result = await _ai.CompleteAsync(messages, ct);
            return result.Content;
        }
        catch
        {
            return locale == "uk"
                ? "Дякую за ваше звернення! Ми розглянемо його найближчим часом."
                : "Thank you for reaching out! We'll look into this shortly.";
        }
    }

    private async Task SaveAssistantMessageAsync(string clientUserId, string text, CancellationToken ct)
    {
        var session = await _conversations.FindOrCreateSessionAsync(clientUserId, ct);
        var entry = ConversationHistoryEntry.Create(session, MessageRole.Assistant, text, DateTimeOffset.UtcNow);
        await _conversations.AddEntryAsync(entry, ct);
        await _conversations.SaveChangesAsync(ct);
    }

    private async Task UpdateDialogStatusAsync(string clientUserId, DialogStatus status, CancellationToken ct)
    {
        var state = await _dialogStates.GetByClientUserIdAsync(clientUserId, ct)
            ?? new DialogState { ClientUserId = clientUserId };
        state.Status = status;
        await _dialogStates.UpsertAsync(state, ct);
        await _dialogStates.SaveChangesAsync(ct);
    }

    private async Task<bool> IsManualModeForClientAsync(string designerUserId, string clientUserId, CancellationToken ct)
    {
        var val = await _memory.GetAsync(designerUserId, $"{ManualModePrefix}{clientUserId}", ct);
        return val is not null;
    }

    private static string StatusIcon(DialogStatus status) => status switch
    {
        DialogStatus.New => "🟡",
        DialogStatus.InProgress => "🔵",
        DialogStatus.Waiting => "⏳",
        DialogStatus.Closed => "⚪",
        _ => "❓"
    };

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
