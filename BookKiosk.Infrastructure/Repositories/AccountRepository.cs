using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Infrastructure.Repositories;

public sealed class AccountRepository(ApplicationDbContext db) : IAccountRepository
{
    public Task<User?> GetAsync(int userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public async Task<bool> TryChangePasswordAsync(int userId, string expectedHash, string newHash, CancellationToken cancellationToken)
    {
        // One conditional SQL UPDATE prevents stale password checks from overwriting a concurrent change/reset.
        var affected = await db.Users
            .Where(u => u.UserId == userId && u.IsActive &&
                EF.Functions.Collate(u.PasswordHash, "Latin1_General_100_BIN2") == expectedHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PasswordHash, newHash), cancellationToken);
        return affected == 1;
    }
}
