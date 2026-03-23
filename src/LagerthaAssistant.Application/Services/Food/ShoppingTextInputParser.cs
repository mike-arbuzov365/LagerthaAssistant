namespace LagerthaAssistant.Application.Services.Food;

using System.Text.RegularExpressions;

public static partial class ShoppingTextInputParser
{
    private static readonly string[] StorePrefixes =
    [
        "store:",
        "shop:",
        "\u043c\u0430\u0433\u0430\u0437\u0438\u043d:",
        "\u043a\u0440\u0430\u043c\u043d\u0438\u0446\u044f:"
    ];

    private static readonly string[] QuantityPrefixes =
    [
        "qty:",
        "q:",
        "\u043a\u0456\u043b\u044c\u043a\u0456\u0441\u0442\u044c:",
        "\u043a-\u0442\u044c:"
    ];

    public static ShoppingTextParseResult Parse(string? input)
    {
        var normalized = NormalizeWhitespace(input);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ShoppingTextParseResult(string.Empty, null, null);
        }

        var remaining = normalized;
        string? store = null;
        string? quantity = null;

        if (TryExtractTrailingStoreToken(ref remaining, out var parsedStore))
        {
            store = parsedStore;
        }

        var segments = ExplicitSeparatorRegex()
            .Split(remaining)
            .Select(NormalizeWhitespace)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (segments.Length > 1)
        {
            var nameParts = new List<string>();
            foreach (var segment in segments)
            {
                if (quantity is null && TryParseQuantitySegment(segment, out var parsedQuantity))
                {
                    quantity = parsedQuantity;
                    continue;
                }

                if (store is null && TryParseStoreSegment(segment, out var parsedSegmentStore))
                {
                    store = parsedSegmentStore;
                    continue;
                }

                nameParts.Add(segment);
            }

            var segmentedName = NormalizeWhitespace(string.Join(' ', nameParts));
            if (!string.IsNullOrWhiteSpace(segmentedName))
            {
                remaining = segmentedName;
            }
        }
        else
        {
            if (quantity is null && TryExtractMarkedValue(ref remaining, QuantityPrefixes, out var markedQuantity))
            {
                quantity = markedQuantity;
            }

            if (store is null && TryExtractMarkedValue(ref remaining, StorePrefixes, out var markedStore))
            {
                store = markedStore;
            }
        }

        if (quantity is null && TryExtractTrailingQuantity(ref remaining, out var trailingQuantity))
        {
            quantity = trailingQuantity;
        }

        var productName = NormalizeWhitespace(remaining);
        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = normalized;
        }

        return new ShoppingTextParseResult(productName, quantity, store);
    }

    private static bool TryParseQuantitySegment(string segment, out string quantity)
    {
        quantity = string.Empty;
        var normalized = NormalizeWhitespace(segment);
        if (TryExtractPrefixedValue(normalized, QuantityPrefixes, out var explicitQuantity))
        {
            quantity = explicitQuantity;
            return true;
        }

        if (TryExtractQuantityFromXPrefix(normalized, out var xQuantity))
        {
            quantity = xQuantity;
            return true;
        }

        if (!QuantityTokenRegex().IsMatch(normalized))
        {
            return false;
        }

        quantity = normalized;
        return true;
    }

    private static bool TryParseStoreSegment(string segment, out string store)
    {
        store = string.Empty;
        var normalized = NormalizeWhitespace(segment);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith('@'))
        {
            var value = NormalizeWhitespace(normalized[1..]);
            if (!string.IsNullOrWhiteSpace(value))
            {
                store = value;
                return true;
            }
        }

        if (!TryExtractPrefixedValue(normalized, StorePrefixes, out var prefixedStore))
        {
            return false;
        }

        store = prefixedStore;
        return true;
    }

    private static bool TryExtractMarkedValue(ref string text, IReadOnlyList<string> prefixes, out string value)
    {
        value = string.Empty;
        var normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var prefix in prefixes)
        {
            var markerIndex = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var rawValue = normalized[(markerIndex + prefix.Length)..];
            var markerStart = markerIndex > 0 ? normalized[..markerIndex] : string.Empty;
            var parsedValue = NormalizeWhitespace(rawValue);
            if (string.IsNullOrWhiteSpace(parsedValue))
            {
                continue;
            }

            text = TrimTrailingSeparators(markerStart);
            value = parsedValue;
            return true;
        }

        return false;
    }

    private static bool TryExtractPrefixedValue(string text, IReadOnlyList<string> prefixes, out string value)
    {
        value = string.Empty;
        foreach (var prefix in prefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawValue = NormalizeWhitespace(text[prefix.Length..]);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            value = rawValue;
            return true;
        }

        return false;
    }

    private static bool TryExtractQuantityFromXPrefix(string text, out string quantity)
    {
        quantity = string.Empty;
        if (!text.StartsWith("x", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("\u00D7", StringComparison.Ordinal))
        {
            return false;
        }

        var raw = NormalizeWhitespace(text[1..]);
        if (!QuantityTokenRegex().IsMatch(raw))
        {
            return false;
        }

        quantity = raw;
        return true;
    }

    private static bool TryExtractTrailingQuantity(ref string text, out string quantity)
    {
        quantity = string.Empty;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        var candidate = NormalizeWhitespace(tokens[^1]);
        if (!QuantityTokenRegex().IsMatch(candidate))
        {
            return false;
        }

        quantity = candidate;
        text = NormalizeWhitespace(string.Join(' ', tokens[..^1]));
        return true;
    }

    private static bool TryExtractTrailingStoreToken(ref string text, out string store)
    {
        store = string.Empty;
        var match = TrailingStoreRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var parsedStore = NormalizeWhitespace(match.Groups["store"].Value);
        if (string.IsNullOrWhiteSpace(parsedStore))
        {
            return false;
        }

        store = parsedStore;
        text = NormalizeWhitespace(text[..match.Index]);
        return true;
    }

    private static string NormalizeWhitespace(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");

    private static string TrimTrailingSeparators(string value)
    {
        var trimmed = value.TrimEnd();
        while (trimmed.EndsWith('|') || trimmed.EndsWith(';') || trimmed.EndsWith(','))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return NormalizeWhitespace(trimmed);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s*[|;]\s*")]
    private static partial Regex ExplicitSeparatorRegex();

    [GeneratedRegex(@"^\d+(?:[.,]\d+)?(?:\s?(?:%|[a-zA-Z\u0430-\u044f\u0410-\u042f\u0456\u0457\u0454\u0491\u0490]{1,8}))?$")]
    private static partial Regex QuantityTokenRegex();

    [GeneratedRegex(@"(?:^|\s)@(?<store>[^|;]+)$")]
    private static partial Regex TrailingStoreRegex();
}
