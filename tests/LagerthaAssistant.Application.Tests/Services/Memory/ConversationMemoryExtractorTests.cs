namespace LagerthaAssistant.Application.Tests.Services.Memory;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Services.Memory;
using Xunit;

public sealed class ConversationMemoryExtractorTests
{
    private readonly ConversationMemoryExtractor _sut = new();

    [Fact]
    public void ExtractFromUserMessage_ShouldExtractEnglishName()
    {
        var facts = _sut.ExtractFromUserMessage("Hi, my name is Michael");

        var nameFact = Assert.Single(facts, x => x.Key == MemoryKeys.UserName);
        Assert.Equal("Michael", nameFact.Value);
    }

    [Fact]
    public void ExtractFromUserMessage_ShouldExtractUkrainianName()
    {
        var message = "\u041f\u0440\u0438\u0432\u0456\u0442, \u043c\u0435\u043d\u0435 \u0437\u0432\u0430\u0442\u0438 \u041c\u0438\u0445\u0430\u0439\u043b\u043e";

        var facts = _sut.ExtractFromUserMessage(message);

        var nameFact = Assert.Single(facts, x => x.Key == MemoryKeys.UserName);
        Assert.Equal("\u041c\u0438\u0445\u0430\u0439\u043b\u043e", nameFact.Value);
    }

    [Fact]
    public void ExtractFromUserMessage_ShouldExtractLanguage()
    {
        var message = "Please answer in English";

        var facts = _sut.ExtractFromUserMessage(message);

        var languageFact = Assert.Single(facts, x => x.Key == MemoryKeys.PreferredLanguage);
        Assert.Equal("en", languageFact.Value);
    }
    [Fact]
    public void ExtractFromUserMessage_ShouldExtractUkrainianLanguageFromCyrillicText()
    {
        var message = "\u0411\u0443\u0434\u044c \u043b\u0430\u0441\u043a\u0430, \u0432\u0456\u0434\u043f\u043e\u0432\u0456\u0434\u0430\u0439 \u0443\u043a\u0440\u0430\u0457\u043d\u0441\u044c\u043a\u043e\u044e";

        var facts = _sut.ExtractFromUserMessage(message);

        var languageFact = Assert.Single(facts, x => x.Key == MemoryKeys.PreferredLanguage);
        Assert.Equal("uk", languageFact.Value);
    }

    [Fact]
    public void ExtractFromUserMessage_ShouldNotThrowOnRandomUnicode()
    {
        var message = "\u30c6\u30b9\u30c8 \u043f\u0440\u0438\u0432\u0435\u0442 ????? ??";

        var exception = Record.Exception(() => _sut.ExtractFromUserMessage(message));

        Assert.Null(exception);
    }
}

