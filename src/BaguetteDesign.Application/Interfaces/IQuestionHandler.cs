namespace BaguetteDesign.Application.Interfaces;

public interface IQuestionHandler
{
    Task HandleAsync(
        long chatId,
        long userId,
        string text,
        string? languageCode,
        CancellationToken cancellationToken = default);
}
