namespace LagerthaAssistant.Application.Models.Vocabulary;

using LagerthaAssistant.Application.Models.AI;

public sealed record VocabularyWorkflowItemResult(
    string Input,
    VocabularyLookupResult Lookup,
    AssistantCompletionResult? AssistantCompletion = null,
    VocabularyAppendPreviewResult? AppendPreview = null)
{
    public bool FoundInDeck => Lookup.Found;

    public bool GeneratedByAssistant => AssistantCompletion is not null;
}
