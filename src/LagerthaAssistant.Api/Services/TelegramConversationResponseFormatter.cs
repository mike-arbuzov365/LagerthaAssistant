using System.Text;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Models.Agents;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramConversationResponseFormatter : ITelegramConversationResponseFormatter
{
    private const string BatchItemMarker = "\uD83D\uDD39";
    private const string BatchSeparator = "--------------------";
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
            builder.Append(BatchItemMarker).Append(' ').AppendLine(item.Input);
            builder.AppendLine();
            var formattedItem = RemoveLeadingInputDuplicate(item.Input, FormatItem(item));
            builder.AppendLine(formattedItem);

            if (index < result.Items.Count - 1)
            {
                builder.AppendLine();
                builder.AppendLine(BatchSeparator);
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

    private static string RemoveLeadingInputDuplicate(string input, string formattedItem)
    {
        if (string.IsNullOrWhiteSpace(formattedItem))
        {
            return formattedItem;
        }

        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return formattedItem.Trim();
        }

        var lines = formattedItem
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        if (lines.Length == 0)
        {
            return formattedItem.Trim();
        }

        var firstLine = lines[0].Trim();
        if (!string.Equals(firstLine, normalizedInput, StringComparison.OrdinalIgnoreCase))
        {
            return formattedItem.Trim();
        }

        var remainder = string.Join('\n', lines.Skip(1)).Trim();
        return string.IsNullOrWhiteSpace(remainder)
            ? normalizedInput
            : remainder;
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

