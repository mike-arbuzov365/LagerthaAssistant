namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IGraphAuthService
{
    Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default);

    Task<GraphLoginResult> LoginAsync(
        Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
        CancellationToken cancellationToken = default);

    Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default);

    Task<GraphLoginResult> CompleteLoginAsync(
        GraphDeviceLoginChallenge challenge,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
