namespace BaguetteDesign.Application.Interfaces;

public interface IStatusHandler
{
    Task ShowStatusAsync(long chatId, long userId, string? languageCode, CancellationToken cancellationToken = default);
}
