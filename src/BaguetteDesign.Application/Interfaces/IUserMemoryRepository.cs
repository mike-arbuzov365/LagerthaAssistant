namespace BaguetteDesign.Application.Interfaces;

public interface IUserMemoryRepository
{
    Task<string?> GetAsync(string userId, string key, CancellationToken cancellationToken = default);
    Task SetAsync(string userId, string key, string value, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string key, CancellationToken cancellationToken = default);
}
