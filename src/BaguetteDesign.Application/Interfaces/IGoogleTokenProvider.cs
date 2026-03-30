namespace BaguetteDesign.Application.Interfaces;

public interface IGoogleTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
