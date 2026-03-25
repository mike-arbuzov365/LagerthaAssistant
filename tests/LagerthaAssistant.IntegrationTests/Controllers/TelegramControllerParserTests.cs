namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Controllers;
using Xunit;

public sealed class TelegramControllerParserTests
{
    // ── Plain format ────────────────────────────────────────────────────────

    [Fact]
    public void TryParseInventoryCartSelection_PlainId_ShouldParseIdOnly()
    {
        var result = TelegramController.TryParseInventoryCartSelection("45", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Null(qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_PlainIdWithQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("45 2", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Equal("2", qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_PlainIdWithUnitQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("45 2kg", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Equal("2kg", qty);
    }

    // ── Bracketed format ────────────────────────────────────────────────────

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdOnly_ShouldParseId()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[45]", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Null(qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdWithTrailingQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[45] 2", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Equal("2", qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdWithEmojiNameAndQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[45] \U0001f95b Milk 2", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Equal("2", qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdWithEmojiNameAndUnitQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[45] \U0001f95b Milk 2kg", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Equal("2kg", qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdWithEmojiNameOnly_ShouldParseIdWithNoQuantity()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[45] \U0001f95b Milk", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(45, id);
        Assert.Null(qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_BracketedIdWithDecimalQuantity_ShouldParseBoth()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[12] \U0001f9c8 Butter 1.5", out var id, out var qty);
        Assert.True(result);
        Assert.Equal(12, id);
        Assert.Equal("1.5", qty);
    }

    // ── Invalid inputs ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseInventoryCartSelection_EmptyOrNull_ShouldReturnFalse(string? input)
    {
        var result = TelegramController.TryParseInventoryCartSelection(input!, out var id, out var qty);
        Assert.False(result);
        Assert.Equal(0, id);
        Assert.Null(qty);
    }

    [Fact]
    public void TryParseInventoryCartSelection_EmptyBrackets_ShouldReturnFalse()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[]", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseInventoryCartSelection_NonNumericBrackets_ShouldReturnFalse()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[abc]", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseInventoryCartSelection_ZeroId_ShouldReturnFalse()
    {
        var result = TelegramController.TryParseInventoryCartSelection("[0] Milk", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseInventoryCartSelection_NegativeId_ShouldReturnFalse()
    {
        var result = TelegramController.TryParseInventoryCartSelection("-5 2", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseInventoryCartSelection_TextOnly_ShouldReturnFalse()
    {
        var result = TelegramController.TryParseInventoryCartSelection("Milk 2kg", out _, out _);
        Assert.False(result);
    }
}
