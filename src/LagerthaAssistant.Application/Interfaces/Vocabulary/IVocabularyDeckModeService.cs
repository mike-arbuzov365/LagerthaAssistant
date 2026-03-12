namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyDeckModeService
{
    Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        VocabularyStorageMode mode,
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default);
}
