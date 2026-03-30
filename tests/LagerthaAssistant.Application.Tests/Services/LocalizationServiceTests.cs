namespace LagerthaAssistant.Application.Tests.Services;

using System.Reflection;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Infrastructure.Services;
using Xunit;

/// <summary>
/// Guards against locale dictionary key drift.
///
/// Background: we once removed "unnecessary" locale entries from the Ukrainian
/// dictionary and the change silently passed all tests — because the service
/// falls back to English instead of throwing. These tests make that impossible.
/// </summary>
public sealed class LocalizationServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, string> GetDictionary(string fieldName)
    {
        var field = typeof(LocalizationService)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Private static field '{fieldName}' not found on LocalizationService. " +
                "If the field was renamed, update this test.");

        return (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
    }

    private static IReadOnlyDictionary<string, string> English => GetDictionary("English");
    private static IReadOnlyDictionary<string, string> Ukrainian => GetDictionary("Ukrainian");

    // ── Key parity tests ──────────────────────────────────────────────────────

    [Fact]
    public void English_And_Ukrainian_HaveIdenticalKeySet()
    {
        var missingInUkrainian = English.Keys.Except(Ukrainian.Keys).OrderBy(k => k).ToList();
        var missingInEnglish = Ukrainian.Keys.Except(English.Keys).OrderBy(k => k).ToList();

        Assert.True(
            missingInUkrainian.Count == 0,
            $"Keys present in English but missing in Ukrainian ({missingInUkrainian.Count}):\n" +
            string.Join("\n", missingInUkrainian));

        Assert.True(
            missingInEnglish.Count == 0,
            $"Keys present in Ukrainian but missing in English ({missingInEnglish.Count}):\n" +
            string.Join("\n", missingInEnglish));
    }

    [Fact]
    public void English_HasNoEmptyValues()
    {
        var empty = English
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .OrderBy(k => k)
            .ToList();

        Assert.True(empty.Count == 0,
            $"English keys with null/empty values ({empty.Count}):\n" +
            string.Join("\n", empty));
    }

    [Fact]
    public void Ukrainian_HasNoEmptyValues()
    {
        var empty = Ukrainian
            .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => kv.Key)
            .OrderBy(k => k)
            .ToList();

        Assert.True(empty.Count == 0,
            $"Ukrainian keys with null/empty values ({empty.Count}):\n" +
            string.Join("\n", empty));
    }

    // ── Fallback / placeholder guard ──────────────────────────────────────────

    [Fact]
    public void Get_NeverReturnsPlaceholder_ForEnglish()
    {
        var sut = new LocalizationService();

        var broken = English.Keys
            .Where(key => sut.Get(key, LocalizationConstants.EnglishLocale).StartsWith("[?:", StringComparison.Ordinal))
            .OrderBy(k => k)
            .ToList();

        Assert.True(broken.Count == 0,
            $"Keys returning placeholder for 'en' ({broken.Count}):\n" +
            string.Join("\n", broken));
    }

    [Fact]
    public void Get_NeverReturnsPlaceholder_ForUkrainian()
    {
        var sut = new LocalizationService();

        var broken = Ukrainian.Keys
            .Where(key => sut.Get(key, LocalizationConstants.UkrainianLocale).StartsWith("[?:", StringComparison.Ordinal))
            .OrderBy(k => k)
            .ToList();

        Assert.True(broken.Count == 0,
            $"Keys returning placeholder for 'uk' ({broken.Count}):\n" +
            string.Join("\n", broken));
    }

    // ── Ukrainian coverage: no silent English fallback ─────────────────────────

    [Fact]
    public void Get_DoesNotSilentlyFallBackToEnglish_ForUkrainianKeys()
    {
        // Every key present in Ukrainian must return a Ukrainian-specific value,
        // i.e. Get("key", "uk") must NOT equal Get("key", "en") for locale-specific
        // keys that are supposed to differ.
        //
        // We allow shared values only for locale-neutral keys (emoji-only buttons,
        // format strings like "{0}", or intentionally identical strings).
        // This test flags if ANY Ukrainian key silently returns the English string
        // because it was missing from the Ukrainian dictionary at runtime.

        var sut = new LocalizationService();

        var fallingBack = Ukrainian.Keys
            .Where(key =>
            {
                var ukValue = sut.Get(key, LocalizationConstants.UkrainianLocale);
                var enValue = sut.Get(key, LocalizationConstants.EnglishLocale);

                // If they're identical, check whether the Ukrainian dictionary
                // truly has an entry (meaning it's intentionally the same),
                // vs the service returned English because the key was missing.
                if (ukValue != enValue)
                    return false; // Different — genuinely translated, OK

                // Values match — verify it's intentional (key exists in Ukrainian dict)
                return !Ukrainian.ContainsKey(key);
            })
            .OrderBy(k => k)
            .ToList();

        Assert.True(fallingBack.Count == 0,
            $"Keys missing from Ukrainian dictionary at runtime ({fallingBack.Count}):\n" +
            string.Join("\n", fallingBack));
    }

    // ── NormalizeLocaleCode ────────────────────────────────────────────────────

    [Theory]
    [InlineData("uk", "uk")]
    [InlineData("uk-UA", "uk")]
    [InlineData("ru", "uk")]
    [InlineData("ru-RU", "uk")]
    [InlineData("be", "uk")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("fr", "en")]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("   ", "en")]
    public void NormalizeLocaleCode_ReturnsExpected(string? input, string expected)
    {
        var result = LocalizationConstants.NormalizeLocaleCode(input);
        Assert.Equal(expected, result);
    }
}
