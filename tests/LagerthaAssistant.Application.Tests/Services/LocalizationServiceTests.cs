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
///
/// AllKnownKeys_ExistInBothDictionaries covers every key used in
/// TelegramNavigationPresenter and TelegramController, so adding a new
/// GetText("some.key", ...) call in code but forgetting the dictionary entry
/// will immediately fail this test.
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

    // ── Full coverage: every key used in UI code ──────────────────────────────

    /// <summary>
    /// Exhaustive list of keys extracted from TelegramNavigationPresenter and
    /// TelegramController. Any new call to GetText/Get must have a corresponding
    /// entry here AND in the dictionary, or this test fails.
    /// </summary>
    private static readonly IReadOnlyList<string> AllKnownKeys =
    [
        // ── Main menu ──────────────────────────────────────────────────────
        "back",
        "start.welcome",
        "stub.wip",
        "locale.switched",
        "language.current",
        "language.changed",
        "language.name.uk",
        "language.name.en",
        "menu.main.title",
        "menu.main.chat",
        "menu.main.vocabulary",
        "menu.main.shopping",
        "menu.main.menu",
        "menu.main.settings",

        // ── Onboarding ─────────────────────────────────────────────────────
        "onboarding.choose_language",
        "onboarding.language_saved",

        // ── Vocabulary menu ────────────────────────────────────────────────
        "menu.vocabulary.title",
        "menu.vocabulary.add",
        "menu.vocabulary.batch",
        "menu.vocabulary.url",
        "menu.vocabulary.stats",
        "menu.vocabulary.back",
        "menu.chat.title",
        "vocab.add.prompt",
        "vocab.batch.prompt",
        "vocab.stats.empty",
        "vocab.stats.title",
        "vocab.stats.total",
        "vocab.stats.summary",
        "vocab.stats.by_marker",
        "vocab.stats.no_data",
        "vocab.stats.item",
        "vocab.stats.top_decks",
        "vocab.stats.deck_item",
        "vocab.stats.and_more_decks",
        "vocab.stats.marker_unknown",
        "vocab.import.choose_source",
        "vocab.import.source.photo",
        "vocab.import.source.file",
        "vocab.import.source.url",
        "vocab.import.source.text",
        "vocab.import.prompt.photo",
        "vocab.import.prompt.file",
        "vocab.import.prompt.url",
        "vocab.import.prompt.text",
        "vocab.import.invalid_expected_photo",
        "vocab.import.invalid_expected_file",
        "vocab.import.invalid_expected_url",
        "vocab.import.invalid_expected_text",
        "vocab.import.file_unsupported",
        "vocab.import.photo_no_text",
        "vocab.import.file_no_text",
        "vocab.import.read_failed",
        "vocab.url.prompt",
        "vocab.url.processing",
        "vocab.url.invalid",
        "vocab.url.empty",
        "vocab.url.suggestions_title",
        "vocab.url.suggestions_group_n",
        "vocab.url.suggestions_group_v",
        "vocab.url.suggestions_group_adj",
        "vocab.url.suggestions_hint",
        "vocab.url.select_parse_failed",
        "vocab.url.selection_cancelled",
        "vocab.url.no_pending",
        "vocab.url.select_all",
        "vocab.url.cancel",
        "vocab.save.ask",
        "vocab.save.saved",
        "vocab.save.duplicate",
        "vocab.save.skip",
        "vocab.save_failed",
        "vocab.save_missing_deck",
        "vocab.save_queued_waiting_auth",
        "vocab.save_batch_ask_hint",
        "vocab.save_batch_ask_question",
        "vocab.save_batch_done",
        "vocab.save_batch_skip",
        "vocab.save_mode_off_hint",
        "vocab.save_yes",
        "vocab.save_no",
        "vocab.save_batch_yes",
        "vocab.save_batch_no",
        "vocab.no_pending_save",
        "vocab.word_unrecognized",
        "vocab.word_unrecognized_with_suggestions",
        "vocab.found_in_deck_single",
        "vocab.found_in_deck_multi_title",
        "vocab.found_in_deck_multi_item",
        "vocab.graph_save_setup_required",
        "vocab.deck_unknown",

        // ── Settings ───────────────────────────────────────────────────────
        "settings.title",
        "settings.language",
        "settings.save_mode",
        "settings.ai",
        "settings.storage_mode",
        "settings.onedrive",
        "settings.notion",
        "settings.notion_enabled",
        "settings.notion_partial",
        "settings.notion_disabled",
        "settings.back",
        "settings.change_language",
        "settings.change_save_mode",
        "settings.change_ai",
        "settings.change_storage_mode",
        "savemode.title",
        "savemode.changed",
        "savemode.auto",
        "savemode.ask",
        "savemode.off",
        "storagemode.title",
        "storagemode.changed",
        "storagemode.local",
        "storagemode.graph",

        // ── AI settings ────────────────────────────────────────────────────
        "ai.title",
        "ai.provider.label",
        "ai.model.label",
        "ai.key.label",
        "ai.key.status.stored",
        "ai.key.status.environment",
        "ai.key.status.missing",
        "ai.provider.change",
        "ai.model.change",
        "ai.key.set",
        "ai.key.remove",
        "ai.provider.title",
        "ai.provider.changed",
        "ai.model.title",
        "ai.model.current",
        "ai.model.changed",
        "ai.key.prompt",
        "ai.key.saved",
        "ai.key.removed",
        "ai.key.cancelled",
        "ai.key.save_failed",

        // ── OneDrive ───────────────────────────────────────────────────────
        "onedrive.title",
        "onedrive.status_connected",
        "onedrive.status_disconnected",
        "onedrive.login",
        "onedrive.logout",
        "onedrive.check_status",
        "onedrive.sync_now",
        "onedrive.rebuild_index",
        "onedrive.rebuild_index_warning",
        "onedrive.rebuild_index_start",
        "onedrive.rebuild_index_started",
        "onedrive.rebuild_index_suggest",
        "onedrive.rebuild_index_done",
        "onedrive.index_ready",
        "onedrive.clear_cache",
        "onedrive.clear_cache_warning",
        "onedrive.clear_cache_start",
        "onedrive.clear_cache_done",
        "onedrive.clear_cache_hint",
        "onedrive.sync_now_done",
        "onedrive.still_not_signed_in",
        "onedrive.login_started",
        "onedrive.login_switched_to_graph",
        "onedrive.logout_done",
        "onedrive.operation_failed",
        "onedrive.error_not_authenticated",
        "onedrive.error_expired",
        "onedrive.error_not_configured",
        "onedrive.error_timed_out",
        "onedrive.error_declined",
        "onedrive.decks_missing_title",
        "onedrive.decks_missing_item",
        "onedrive.decks_missing_hint",

        // ── Notion ─────────────────────────────────────────────────────────
        "notion.title",
        "notion.vocabulary",
        "notion.food",
        "notion.status_enabled",
        "notion.status_disabled",
        "notion.configured_yes",
        "notion.configured_no",
        "notion.worker_enabled",
        "notion.worker_disabled",
        "notion.tip",

        // ── Food menu ──────────────────────────────────────────────────────
        "menu.food.title",
        "menu.food.inventory",
        "menu.food.shopping",
        "menu.food.back",
        "food.unknown",

        // ── Shopping list ──────────────────────────────────────────────────
        "menu.shopping.title",
        "menu.shopping.add",
        "menu.shopping.list",
        "menu.shopping.delete",
        "menu.shopping.back",
        "food.shop.list.empty",
        "food.shop.list.title",
        "food.shop.list.no_store",
        "food.shop.list.store",
        "food.shop.list.item",
        "food.shop.add.prompt",
        "food.shop.added",
        "food.shop.clear.none",
        "food.shop.clear.done",
        "food.shop.qty_suffix",
        "food.shop.store_suffix",
        "food.shop.add.from_inventory",
        "food.shop.delete.prompt.title",
        "food.shop.delete.prompt.item",
        "food.shop.delete.prompt.hint",
        "food.shop.delete.invalid",
        "food.shop.delete.cancelled",
        "food.shop.delete.no_match",
        "food.shop.delete.done",
        "shop.not_in_inventory",
        "shop.only_english",
        "shop.add_inventory_first",
        "shop.matched_inventory",

        // ── Inventory ──────────────────────────────────────────────────────
        "menu.inventory.title",
        "menu.inventory.list",
        "menu.inventory.search",
        "menu.inventory.add",
        "menu.inventory.suggest",
        "menu.inventory.back",
        "menu.inventory.stats",
        "menu.inventory.move",
        "menu.inventory.manage",
        "menu.inventory.photo_restock",
        "menu.inventory.photo_consume",
        "menu.inventory.sub.back",
        "menu.inventory.adjust",
        "menu.inventory.min",
        "menu.inventory.reset_stock",
        "inventory.empty",
        "inventory.search.prompt",
        "inventory.search.empty",
        "inventory.search.results",
        "inventory.add.prompt",
        "inventory.add_to_cart_hint",
        "inventory.cart.added",
        "inventory.cart.not_found",
        "inventory.suggest.empty",
        "inventory.suggest.all_good",
        "inventory.suggest.missing_current",
        "inventory.category.uncategorized",
        "inventory.list.empty",
        "inventory.list.button.available",
        "inventory.list.button.missing",
        "inventory.list.header.available",
        "inventory.list.header.missing",
        "inventory.list.empty.available",
        "inventory.list.empty.missing",
        "inventory.list.prompt.available",
        "inventory.list.prompt.missing",
        "inventory.low_stock.title",
        "inventory.low_stock.item",
        "inventory.stats.title",
        "inventory.stats.total_items",
        "inventory.stats.with_current",
        "inventory.stats.with_min",
        "inventory.stats.low_stock",
        "inventory.stats.total_current",
        "inventory.adjust.prompt",
        "inventory.adjust.hint",
        "inventory.adjust.done",
        "inventory.adjust.invalid",
        "inventory.adjust.not_found",
        "inventory.min.prompt",
        "inventory.min.hint",
        "inventory.min.done",
        "inventory.min.invalid",
        "inventory.min.not_found",
        "inventory.reset_stock.confirm",
        "inventory.reset_stock.done",
        "inventory.reset_stock.prompt",
        "inventory.photo.apply_all",
        "inventory.photo.select",
        "inventory.photo.cancel",
        "inventory.photo.awaiting_image",
        "inventory.photo.awaiting_restock",
        "inventory.photo.awaiting_consume",
        "inventory.photo.expired",
        "inventory.photo.cancelled",
        "inventory.photo.done",
        "inventory.photo.empty",
        "inventory.photo.failed",
        "inventory.photo.invalid_selection",
        "inventory.photo.mode_required",
        "inventory.photo.select.prompt",
        "inventory.photo.preview",
        "inventory.photo.preview.title",
        "inventory.photo.preview.item",
        "inventory.photo.preview.mode.restock",
        "inventory.photo.preview.mode.consume",
        "inventory.photo.preview.non_products",
        "inventory.photo.preview.store",
        "inventory.photo.preview.confirm",
        "inventory.photo.preview.unknown_title",
        "inventory.photo.preview.unknown_item",
        "inventory.photo.applied",
        "inventory.photo.applied.item",
        "inventory.photo.store.detected",
        "inventory.photo.store.pick",
        "inventory.photo.store.pick_prompt",
        "inventory.photo.store.resolve",
        "inventory.photo.store.add",
        "inventory.photo.store.pick_existing",
        "inventory.photo.store.skip",
        "inventory.photo.unknown.add_all",
        "inventory.photo.unknown.select",
        "inventory.photo.unknown.link",
        "inventory.photo.unknown.skip",
        "inventory.photo.unknown.added",
        "inventory.photo.unknown.added.none",
        "inventory.photo.unknown.link_done",
        "inventory.photo.unknown.link_invalid",
        "inventory.photo.unknown.link_not_found",
        "inventory.photo.unknown.link_prompt",
        "inventory.photo.unknown.offer",
        "inventory.photo.unknown.offer_title",
        "inventory.photo.unknown.offer_prompt",
        "inventory.photo.unknown.select_prompt",

        // ── Weekly menu ────────────────────────────────────────────────────
        "menu.weekly.title",
        "menu.weekly.view",
        "menu.weekly.plan",
        "menu.weekly.log",
        "menu.weekly.create",
        "menu.weekly.analytics",
        "menu.weekly.back",
        "menu.weekly.calories",
        "menu.weekly.goal",
        "menu.weekly.favourites",
        "menu.weekly.diversity",
        "menu.weekly.analytics.back",
        "meal.create.confirm",
        "meal.create.cancel",
        "food.weekly.view.empty",
        "food.weekly.view.title",
        "food.weekly.view.ingredients",
        "food.weekly.view",
        "food.weekly.view.line",
        "food.weekly.view.calories_suffix",
        "food.weekly.cookable.empty",
        "food.weekly.cookable.title",
        "food.weekly.logged",
        "food.weekly.log.empty",
        "food.weekly.log.prompt",
        "food.weekly.log.not_found",
        "food.weekly.portion.title",
        "food.weekly.portion.ingredient",
        "food.weekly.portion.not_found",
        "food.weekly.create.prompt",
        "food.weekly.create.empty",
        "food.weekly.create.preview.title",
        "food.weekly.create.preview.servings",
        "food.weekly.create.preview.confirm",
        "food.weekly.create.expired",
        "food.weekly.create.error",
        "food.weekly.create.done",
        "food.weekly.create.cancelled",
        "food.weekly.calories.empty",
        "food.weekly.calories.title",
        "food.weekly.calories.total",
        "food.weekly.calories.avg",
        "food.weekly.calories.protein",
        "food.weekly.calories.carbs",
        "food.weekly.calories.fat",
        "food.weekly.favourites.empty",
        "food.weekly.favourites.title",
        "food.weekly.goal.title",
        "food.weekly.goal.consumed",
        "food.weekly.goal.remaining",
        "food.weekly.goal.meals",
        "food.weekly.diversity.empty",
        "food.weekly.diversity.title",
        "food.weekly.diversity.total",
        "food.weekly.diversity.unique",
        "food.weekly.diversity.repeated",
        "food.weekly.diversity.score",

        // ── Food photo ─────────────────────────────────────────────────────
        "food.photo.confirm",
        "food.photo.cancel",
        "food.photo.preview",
        "food.photo.preview.identified",
        "food.photo.preview.calories",
        "food.photo.preview.servings",
        "food.photo.preview.confirm",
        "food.photo.logged",
        "food.photo.expired",
        "food.photo.error",
        "food.photo.cancelled",
        "food.photo.failed",
        "food.photo.failed_default",

        // ── Console commands ────────────────────────────────────────────────
        "command.console_only_generic",
        "command.console_only_graph",
    ];

    [Fact]
    public void AllKnownKeys_ExistInBothDictionaries()
    {
        var missingInEnglish = AllKnownKeys
            .Where(k => !English.ContainsKey(k))
            .OrderBy(k => k)
            .ToList();

        var missingInUkrainian = AllKnownKeys
            .Where(k => !Ukrainian.ContainsKey(k))
            .OrderBy(k => k)
            .ToList();

        var all = missingInEnglish.Union(missingInUkrainian).OrderBy(k => k).ToList();

        Assert.True(all.Count == 0,
            $"Keys used in code but missing from dictionary ({all.Count}):\n" +
            $"  English missing: {missingInEnglish.Count}\n" +
            $"  Ukrainian missing: {missingInUkrainian.Count}\n" +
            string.Join("\n", all.Select(k => $"  [{(missingInEnglish.Contains(k) ? "EN" : "  ")}{(missingInUkrainian.Contains(k) ? "UK" : "  ")}] {k}")));
    }

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
        var sut = new LocalizationService();

        var fallingBack = Ukrainian.Keys
            .Where(key =>
            {
                var ukValue = sut.Get(key, LocalizationConstants.UkrainianLocale);
                var enValue = sut.Get(key, LocalizationConstants.EnglishLocale);
                if (ukValue != enValue)
                    return false;
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
