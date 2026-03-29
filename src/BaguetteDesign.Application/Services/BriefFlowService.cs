namespace BaguetteDesign.Application.Services;

using System.Text.Json;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class BriefFlowService : IBriefFlowService
{
    private const string StateKey = "brief_state";
    private const string TelegramChannel = "telegram";

    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly IUserMemoryRepository _memory;
    private readonly ILeadRepository _leads;
    private readonly IAiChatClient _aiClient;
    private readonly ITelegramBotSender _sender;

    public BriefFlowService(
        IUserMemoryRepository memory,
        ILeadRepository leads,
        IAiChatClient aiClient,
        ITelegramBotSender sender)
    {
        _memory = memory;
        _leads = leads;
        _aiClient = aiClient;
        _sender = sender;
    }

    public async Task<bool> IsActiveAsync(string userId, CancellationToken cancellationToken = default)
    {
        var raw = await _memory.GetAsync(userId, StateKey, cancellationToken);
        if (raw is null) return false;
        var state = Deserialize(raw);
        return !state.IsCompleted;
    }

    public async Task StartAsync(long chatId, string userId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var state = new BriefFlowState();
        await SaveStateAsync(userId, state, cancellationToken);
        await AskCurrentStepAsync(chatId, state, ResolveLocale(languageCode), cancellationToken);
    }

    public async Task HandleTextAsync(long chatId, string userId, string text, string? languageCode, CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(userId, cancellationToken);
        if (state is null || state.IsCompleted) return;

        var locale = ResolveLocale(languageCode);
        state = state.WithAnswer(text).AdvanceTo(state.NextStep());
        await SaveStateAsync(userId, state, cancellationToken);
        await HandleNextStepAsync(chatId, userId, state, locale, cancellationToken);
    }

    public async Task HandleCallbackAsync(long chatId, string userId, string callbackData, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);

        if (callbackData == "brief_cancel")
        {
            await _memory.DeleteAsync(userId, StateKey, cancellationToken);
            var msg = locale == "uk" ? "Бриф скасовано. Повертайтесь, коли будете готові!" : "Brief cancelled. Come back when ready!";
            await _sender.SendTextAsync(chatId, msg, cancellationToken: cancellationToken);
            return;
        }

        var state = await LoadStateAsync(userId, cancellationToken);
        if (state is null || state.IsCompleted) return;

        if (callbackData == "brief_skip")
        {
            state = state.AdvanceTo(state.NextStep());
            await SaveStateAsync(userId, state, cancellationToken);
            await HandleNextStepAsync(chatId, userId, state, locale, cancellationToken);
            return;
        }

        if (callbackData == "brief_back")
        {
            state = state.AdvanceTo(state.PreviousStep());
            await SaveStateAsync(userId, state, cancellationToken);
            await AskCurrentStepAsync(chatId, state, locale, cancellationToken);
            return;
        }

        if (callbackData == "brief_confirm" && state.CurrentStep == BriefStep.Summary)
        {
            await ConfirmAndSaveLeadAsync(chatId, userId, state, locale, cancellationToken);
            return;
        }

        if (callbackData == "brief_edit" && state.CurrentStep == BriefStep.Summary)
        {
            // Go back to first step to let user redo
            state = state.AdvanceTo(BriefStep.ServiceType);
            await SaveStateAsync(userId, state, cancellationToken);
            await AskCurrentStepAsync(chatId, state, locale, cancellationToken);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task HandleNextStepAsync(long chatId, string userId, BriefFlowState state, string locale, CancellationToken ct)
    {
        if (state.CurrentStep == BriefStep.Summary)
        {
            await GenerateAndShowSummaryAsync(chatId, userId, state, locale, ct);
            return;
        }

        if (state.CurrentStep == BriefStep.Completed) return;

        await AskCurrentStepAsync(chatId, state, locale, ct);
    }

    private async Task AskCurrentStepAsync(long chatId, BriefFlowState state, string locale, CancellationToken ct)
    {
        var (question, keyboard) = BuildStepQuestion(state.CurrentStep, locale, isFirst: state.CurrentStep == BriefStep.ServiceType);
        await _sender.SendTextAsync(
            chatId, question,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: ct);
    }

    private async Task GenerateAndShowSummaryAsync(long chatId, string userId, BriefFlowState state, string locale, CancellationToken ct)
    {
        var summaryText = await BuildAiSummaryAsync(state, locale, ct);
        state = state with { AiSummary = summaryText };
        await SaveStateAsync(userId, state, ct);

        var confirmLabel = locale == "uk" ? "✅ Підтвердити" : "✅ Confirm";
        var editLabel    = locale == "uk" ? "✏️ Змінити" : "✏️ Edit";
        var cancelLabel  = locale == "uk" ? "❌ Скасувати" : "❌ Cancel";

        var intro = locale == "uk"
            ? "📋 <b>Підсумок вашого брифу:</b>\n\n"
            : "📋 <b>Your brief summary:</b>\n\n";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(confirmLabel, "brief_confirm"), Btn(editLabel, "brief_edit") },
                new[] { Btn(cancelLabel, "brief_cancel") }
            }
        };

        await _sender.SendTextAsync(
            chatId, intro + summaryText,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: ct);
    }

    private async Task<string> BuildAiSummaryAsync(BriefFlowState state, string locale, CancellationToken ct)
    {
        var systemPrompt = locale == "uk"
            ? "Ти — асистент дизайн-студії Baguette Design. Склади структурований підсумок брифу клієнта на українській мові. Використай emoji для кожного пункту. Будь лаконічним."
            : "You are an assistant for Baguette Design studio. Write a structured brief summary in English. Use emoji for each point. Be concise.";

        var briefData = locale == "uk"
            ? $"Тип послуги: {state.ServiceType ?? "не вказано"}\nБренд: {state.Brand ?? "не вказано"}\nАудиторія: {state.Audience ?? "не вказано"}\nСтиль: {state.Style ?? "не вказано"}\nДедлайн: {state.Deadline ?? "не вказано"}\nБюджет: {state.Budget ?? "не вказано"}\nКраїна: {state.Country ?? "не вказано"}"
            : $"Service type: {state.ServiceType ?? "not specified"}\nBrand: {state.Brand ?? "not specified"}\nAudience: {state.Audience ?? "not specified"}\nStyle: {state.Style ?? "not specified"}\nDeadline: {state.Deadline ?? "not specified"}\nBudget: {state.Budget ?? "not specified"}\nCountry: {state.Country ?? "not specified"}";

        var messages = new List<SharedBotKernel.Domain.AI.ConversationMessage>
        {
            SharedBotKernel.Domain.AI.ConversationMessage.Create(MessageRole.System, systemPrompt, DateTimeOffset.UtcNow),
            SharedBotKernel.Domain.AI.ConversationMessage.Create(MessageRole.User, briefData, DateTimeOffset.UtcNow)
        };

        var result = await _aiClient.CompleteAsync(messages, ct);
        return result.Content;
    }

    private async Task ConfirmAndSaveLeadAsync(long chatId, string userId, BriefFlowState state, string locale, CancellationToken ct)
    {
        var lead = Lead.FromBriefState(userId, state);
        await _leads.AddAsync(lead, ct);
        await _leads.SaveChangesAsync(ct);

        var completedState = state.AdvanceTo(BriefStep.Completed);
        await SaveStateAsync(userId, completedState, ct);

        var text = locale == "uk"
            ? "🎉 Дякуємо! Ваш бриф прийнято. Дизайнер зв'яжеться з вами найближчим часом."
            : "🎉 Thank you! Your brief has been submitted. The designer will contact you shortly.";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(locale == "uk" ? "💰 Прайс" : "💰 Pricing", "price"),
                        Btn(locale == "uk" ? "🎨 Портфоліо" : "🎨 Portfolio", "portfolio") }
            }
        };

        await _sender.SendTextAsync(chatId, text,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: ct);
    }

    private static (string question, object keyboard) BuildStepQuestion(BriefStep step, string locale, bool isFirst)
    {
        var uk = locale == "uk";
        var skipBtn = Btn(uk ? "⏭ Пропустити" : "⏭ Skip", "brief_skip");
        var backBtn = Btn(uk ? "◀️ Назад" : "◀️ Back", "brief_back");
        var cancelBtn = Btn(uk ? "❌ Скасувати" : "❌ Cancel", "brief_cancel");

        var navRow = isFirst
            ? new[] { cancelBtn }
            : new[] { backBtn, skipBtn };

        return step switch
        {
            BriefStep.ServiceType => (
                uk ? "🎨 <b>Який тип послуги вас цікавить?</b>\n\nОберіть або напишіть своє:"
                   : "🎨 <b>What type of service are you interested in?</b>\n\nChoose or type your own:",
                new
                {
                    inline_keyboard = new[]
                    {
                        new[] { Btn("🏷 Logo", "brief_svc_logo"), Btn("📱 Social Media", "brief_svc_social") },
                        new[] { Btn("🌐 Website", "brief_svc_website"), Btn("💼 Branding", "brief_svc_branding") },
                        new[] { Btn(uk ? "✍️ Інше" : "✍️ Other", "brief_svc_other"), cancelBtn }
                    }
                }),

            BriefStep.Brand => (
                uk ? "🏢 <b>Розкажіть про ваш бренд або проєкт.</b>\n\nЯк він називається? Що робить?"
                   : "🏢 <b>Tell me about your brand or project.</b>\n\nWhat is it called? What does it do?",
                new { inline_keyboard = new[] { navRow } }),

            BriefStep.Audience => (
                uk ? "👥 <b>Хто ваша цільова аудиторія?</b>\n\nВік, інтереси, де живуть?"
                   : "👥 <b>Who is your target audience?</b>\n\nAge, interests, location?",
                new { inline_keyboard = new[] { navRow } }),

            BriefStep.Style => (
                uk ? "✨ <b>Який стиль вам подобається?</b>\n\nМінімалізм, яскравий, корпоративний, playful? Можна кинути приклади."
                   : "✨ <b>What style do you prefer?</b>\n\nMinimalist, bold, corporate, playful? Feel free to share examples.",
                new { inline_keyboard = new[] { navRow } }),

            BriefStep.Deadline => (
                uk ? "📅 <b>Який у вас дедлайн?</b>\n\nКоли потрібен результат?"
                   : "📅 <b>What is your deadline?</b>\n\nWhen do you need the result?",
                new { inline_keyboard = new[] { navRow } }),

            BriefStep.Budget => (
                uk ? "💰 <b>Який ваш бюджет?</b>\n\nВкажіть суму або діапазон у будь-якій валюті."
                   : "💰 <b>What is your budget?</b>\n\nPlease state an amount or range in any currency.",
                new { inline_keyboard = new[] { navRow } }),

            BriefStep.Country => (
                uk ? "🌍 <b>З якої ви країни?</b>\n\nЦе впливає на ціну і умови."
                   : "🌍 <b>Which country are you from?</b>\n\nThis affects pricing and terms.",
                new { inline_keyboard = new[] { navRow } }),

            _ => (string.Empty, new { inline_keyboard = Array.Empty<object[]>() })
        };
    }

    private async Task<BriefFlowState?> LoadStateAsync(string userId, CancellationToken ct)
    {
        var raw = await _memory.GetAsync(userId, StateKey, ct);
        return raw is null ? null : Deserialize(raw);
    }

    private async Task SaveStateAsync(string userId, BriefFlowState state, CancellationToken ct)
        => await _memory.SetAsync(userId, StateKey, JsonSerializer.Serialize(state, JsonOpts), ct);

    private static BriefFlowState Deserialize(string raw)
        => JsonSerializer.Deserialize<BriefFlowState>(raw, JsonOpts) ?? new BriefFlowState();

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
