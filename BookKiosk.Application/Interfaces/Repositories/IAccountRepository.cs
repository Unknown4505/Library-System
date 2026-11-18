using BookKiosk.Domain.Entities;

namespace BookKiosk.Application.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<User?> GetAsync(int userId, CancellationToken cancellationToken);
    Task<bool> TryChangePasswordAsync(int userId, string expectedHash, string newHash, CancellationToken cancellationToken);
}
