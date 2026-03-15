using System.Text;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Models.Agents;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramConversationResponseFormatter : ITelegramConversationResponseFormatter
{
    private readonly int _textLengthLimit;

    public TelegramConversationResponseFormatter(IOptions<TelegramOptions> options)
    {
        _textLengthLimit = options.Value.TextLengthLimit;
    }

    public string Format(ConversationAgentResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return TrimToLimit(result.Message);
        }

        if (result.Items.Count == 0)
        {
            return "Done.";
        }

        if (!result.IsBatch || result.Items.Count == 1)
        {
            return TrimToLimit(FormatItem(result.Items[0]));
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

        return TrimToLimit(builder.ToString());
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

    private string TrimToLimit(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length <= _textLengthLimit)
        {
            return normalized;
        }

        return normalized[..(_textLengthLimit - 3)] + "...";
    }
}
