namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularyLookupResult(
    string Query,
    IReadOnlyList<VocabularyDeckEntry> Matches)
{
    public bool Found => Matches.Count > 0;
}
