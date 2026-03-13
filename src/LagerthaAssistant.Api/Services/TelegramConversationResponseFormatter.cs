using System.Text;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Application.Models.Agents;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramConversationResponseFormatter : ITelegramConversationResponseFormatter
{
    private const int TelegramTextLimit = 3900;

    public string Format(ConversationAgentResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return TrimToTelegramLimit(result.Message);
        }

        if (result.Items.Count == 0)
        {
            return "Done.";
        }

        if (!result.IsBatch || result.Items.Count == 1)
        {
            return TrimToTelegramLimit(FormatItem(result.Items[0]));
        }

        var builder = new StringBuilder();
        for (var index = 0; index < result.Items.Count; index++)
        {
            var item = result.Items[index];
            builder.Append(index + 1).Append(") ").AppendLine(item.Input);
            builder.AppendLine(FormatItem(item));

            if (index < result.Items.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return TrimToTelegramLimit(builder.ToString());
    }

    private static string FormatItem(ConversationAgentItemResult item)
    {
        if (!string.IsNullOrWhiteSpace(item.AssistantCompletion?.Content))
        {
            return item.AssistantCompletion.Content.Trim();
        }

        if (item.Lookup.Found && item.Lookup.Matches.Count > 0)
        {
            var first = item.Lookup.Matches[0];
            var blocks = new List<string> { first.Word };
            if (!string.IsNullOrWhiteSpace(first.Meaning))
            {
                blocks.Add(first.Meaning);
            }

            if (!string.IsNullOrWhiteSpace(first.Examples))
            {
                blocks.Add(first.Examples);
            }

            return string.Join(Environment.NewLine + Environment.NewLine, blocks);
        }

        if (!string.IsNullOrWhiteSpace(item.AppendPreview?.Message))
        {
            return item.AppendPreview.Message.Trim();
        }

        return "Processed.";
    }

    private static string TrimToTelegramLimit(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length <= TelegramTextLimit)
        {
            return normalized;
        }

        return normalized[..(TelegramTextLimit - 3)] + "...";
    }
}
