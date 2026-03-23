namespace LagerthaAssistant.Application.Tests.Services.Food;

using LagerthaAssistant.Application.Services.Food;
using Xunit;

public sealed class ShoppingTextInputParserTests
{
    [Fact]
    public void Parse_ShouldKeepMultiWordProductNameByDefault()
    {
        var result = ShoppingTextInputParser.Parse("sparkling water");

        Assert.Equal("sparkling water", result.ProductName);
        Assert.Null(result.Quantity);
        Assert.Null(result.Store);
    }

    [Fact]
    public void Parse_ShouldExtractQuantityAndStore_WhenExplicitSegmentsProvided()
    {
        var result = ShoppingTextInputParser.Parse("sparkling water | qty:2L | store:ATB");

        Assert.Equal("sparkling water", result.ProductName);
        Assert.Equal("2L", result.Quantity);
        Assert.Equal("ATB", result.Store);
    }

    [Fact]
    public void Parse_ShouldExtractTrailingQuantity_WhenPatternIsUnambiguous()
    {
        var result = ShoppingTextInputParser.Parse("olive oil 1L");

        Assert.Equal("olive oil", result.ProductName);
        Assert.Equal("1L", result.Quantity);
        Assert.Null(result.Store);
    }

    [Fact]
    public void Parse_ShouldTreatAmbiguousTailAsProductName_WhenStoreTokenIsNotExplicit()
    {
        var result = ShoppingTextInputParser.Parse("milk 2L supermart");

        Assert.Equal("milk 2L supermart", result.ProductName);
        Assert.Null(result.Quantity);
        Assert.Null(result.Store);
    }

    [Fact]
    public void Parse_ShouldExtractStoreFromAtToken()
    {
        var result = ShoppingTextInputParser.Parse("whole grain bread @silpo");

        Assert.Equal("whole grain bread", result.ProductName);
        Assert.Null(result.Quantity);
        Assert.Equal("silpo", result.Store);
    }
}
