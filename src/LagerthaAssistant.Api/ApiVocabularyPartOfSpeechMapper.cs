namespace LagerthaAssistant.Api;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Services.Vocabulary;

internal static class ApiVocabularyPartOfSpeechMapper
{
    public static IReadOnlyList<VocabularyPartOfSpeechOptionResponse> BuildOptions()
    {
        return MapOptions(VocabularyPartOfSpeechCatalog.GetOptions());
    }

    public static IReadOnlyList<VocabularyPartOfSpeechOptionResponse> MapOptions(
        IReadOnlyList<VocabularyPartOfSpeechOption> options)
    {
        return options
            .OrderBy(option => option.Number)
            .Select(option => new VocabularyPartOfSpeechOptionResponse(option.Number, option.Marker, option.Label))
            .ToList();
    }
}
