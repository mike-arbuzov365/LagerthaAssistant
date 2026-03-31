namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using SharedBotKernel.Domain.AI;

public sealed class QuestionHandler : IQuestionHandler
{
    private const int HistoryLimit = 20;
    private const string SystemPromptUk =
        "Ти — AI-асистент дизайн-студії Baguette Design. " +
        "Допомагаєш клієнтам отримати інформацію про послуги, процес замовлення дизайну та ціни. " +
        "Відповідай коротко, ввічливо, українською мовою. " +
        "Якщо не знаєш точної відповіді — запропонуй зв'язатися з дизайнером напряму.";

    private const string SystemPromptEn =
        "You are an AI assistant for Baguette Design studio. " +
        "You help clients get information about services, the design ordering process, and pricing. " +
        "Reply concisely and politely in English. " +
        "If you don't know the exact answer — suggest contacting the designer directly.";

    private readonly IAiChatClient _aiClient;
    private readonly IConversationRepository _repo;
    private readonly ITelegramBotSender _sender;

    public QuestionHandler(
        IAiChatClient aiClient,
        IConversationRepository repo,
        ITelegramBotSender sender)
    {
        _aiClient = aiClient;
        _repo = repo;
        _sender = sender;
    }

    public async Task HandleAsync(
        long chatId,
        long userId,
        string text,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var now = DateTimeOffset.UtcNow;

        var session = await _repo.FindOrCreateSessionAsync(userId.ToString(), cancellationToken);
        var history = await _repo.GetRecentHistoryAsync(session.Id, HistoryLimit, cancellationToken);

        var messages = BuildMessages(locale, history, text, now);
        var result = await _aiClient.CompleteAsync(messages, cancellationToken);

        var userEntry = ConversationHistoryEntry.Create(session, MessageRole.User, text, now);
        var assistantEntry = ConversationHistoryEntry.Create(session, MessageRole.Assistant, result.Content, DateTimeOffset.UtcNow);

        await _repo.AddEntryAsync(userEntry, cancellationToken);
        await _repo.AddEntryAsync(assistantEntry, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        var responseText = result.Content;
        var keyboard = BuildNextStepsKeyboard();

        await _sender.SendTextAsync(
            chatId,
            responseText,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    private static IReadOnlyCollection<ConversationMessage> BuildMessages(
        string locale,
        IReadOnlyList<ConversationHistoryEntry> history,
        string userText,
        DateTimeOffset now)
    {
        var systemPrompt = locale == "uk" ? SystemPromptUk : SystemPromptEn;
        var messages = new List<ConversationMessage>
        {
            ConversationMessage.Create(MessageRole.System, systemPrompt, now)
        };

        foreach (var entry in history)
        {
            messages.Add(ConversationMessage.Create(entry.Role, entry.Content, entry.SentAtUtc));
        }

        messages.Add(ConversationMessage.Create(MessageRole.User, userText, now));

        return messages;
    }

    private static object BuildNextStepsKeyboard() =>
        new
        {
            inline_keyboard = new[]
            {
                new[] { Btn("📋 Бриф", "brief"), Btn("💰 Прайс", "price") },
                new[] { Btn("🎨 Портфоліо", "portfolio"), Btn("🔗 Зв'язатися", "contact") }
            }
        };

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
