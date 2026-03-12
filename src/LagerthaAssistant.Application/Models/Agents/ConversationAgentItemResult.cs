namespace LagerthaAssistant.Application.Models.Agents;

using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed record ConversationAgentItemResult(
    string Input,
    VocabularyLookupResult Lookup,
    AssistantCompletionResult? AssistantCompletion = null,
    VocabularyAppendPreviewResult? AppendPreview = null)
{
    public bool FoundInDeck => Lookup.Found;
}
