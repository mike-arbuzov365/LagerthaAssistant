namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyWorkflowService
{
    Task<VocabularyWorkflowItemResult> ProcessAsync(
        string input,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
