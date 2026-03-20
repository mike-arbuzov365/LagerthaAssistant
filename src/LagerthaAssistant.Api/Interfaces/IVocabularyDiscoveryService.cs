namespace LagerthaAssistant.Api.Interfaces;

public interface IVocabularyDiscoveryService
{
    Task<VocabularyDiscoveryResult> DiscoverAsync(
        string sourceInput,
        CancellationToken cancellationToken = default);
}

public enum VocabularyDiscoveryStatus
{
    Success = 0,
    InvalidSource = 1,
    NoCandidates = 2,
    Failed = 3
}

public sealed record VocabularyDiscoveryCandidate(
    string Word,
    string PartOfSpeech,
    int Frequency);

public sealed record VocabularyDiscoveryResult(
    VocabularyDiscoveryStatus Status,
    IReadOnlyList<VocabularyDiscoveryCandidate> Candidates,
    string Message,
    bool SourceWasUrl = false);
