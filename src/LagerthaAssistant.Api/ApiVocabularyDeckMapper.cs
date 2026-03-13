namespace LagerthaAssistant.Api;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;

internal static class ApiVocabularyDeckMapper
{
    public static IReadOnlyList<VocabularyDeckInfoResponse> MapDecks(
        IReadOnlyList<VocabularyDeckFile> decks)
    {
        return decks
            .OrderBy(deck => deck.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(MapDeck)
            .ToList();
    }

    public static VocabularyDeckInfoResponse MapDeck(VocabularyDeckFile deck)
    {
        return new VocabularyDeckInfoResponse(
            deck.FileName,
            deck.FullPath,
            VocabularyDeckMarkerSuggester.SuggestMarker(deck.FileName));
    }
}
