namespace BaguetteDesign.Application.Interfaces;

public interface IStartCommandHandler
{
    Task HandleAsync(long chatId, long userId, string? languageCode, CancellationToken cancellationToken = default);
}
