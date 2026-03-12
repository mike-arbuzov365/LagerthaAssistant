namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularyReplyParserTests
{
    private readonly VocabularyReplyParser _sut = new();

    [Fact]
    public void TryParse_ShouldParseWordMeaningsAndExamples()
    {
        const string response = """
void

(n) порожнеча, відсутність значення або вмісту
(v) робити порожнім, звільняти від вмісту

The function returns void when there is no value to return.
Make sure to void the buffer before reusing it.
""";

        var parsed = _sut.TryParse(response, out var result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal("void", result!.Word);
        Assert.Equal(2, result.Meanings.Count);
        Assert.Equal(2, result.Examples.Count);
        Assert.Equal(["n", "v"], result.PartsOfSpeech);
    }

    [Fact]
    public void TryParse_ShouldParseIrregularVerbHeader()
    {
        const string response = """
beat - beat - beaten

(iv) бити

I beat the deadline every sprint.

The old system has been beaten by the new one.
""";

        var parsed = _sut.TryParse(response, out var result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal("beat - beat - beaten", result!.Word);
        Assert.Equal(["iv"], result.PartsOfSpeech);
        Assert.Equal(2, result.Examples.Count);
    }

    [Fact]
    public void TryParse_ShouldHandleCodeFenceResponse()
    {
        const string response = """
```text
layer

(n) шар

We need to add one more layer.
```
""";

        var parsed = _sut.TryParse(response, out var result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal("layer", result!.Word);
        Assert.Single(result.Meanings);
        Assert.Single(result.Examples);
    }

    [Fact]
    public void TryParse_ShouldAllowPersistentExpressionWithoutExamples()
    {
        const string response = """
On the same page

(pe) мати спільне розуміння
""";

        var parsed = _sut.TryParse(response, out var result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal("on the same page", result!.Word);
        Assert.Equal(["pe"], result.PartsOfSpeech);
        Assert.Empty(result.Examples);
    }
    [Fact]
    public void TryParse_ShouldReturnFalse_WhenFormatIsInvalid()
    {
        const string response = "hello there";

        var parsed = _sut.TryParse(response, out var result);

        Assert.False(parsed);
        Assert.Null(result);
    }
}
