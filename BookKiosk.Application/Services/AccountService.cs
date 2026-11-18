using System.Text;
using BookKiosk.Application.DTOs.Auth;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Application.Services;

public interface IAccountService
{
    Task<UserDto> GetProfileAsync(int userId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(int userId, ChangeOwnPasswordDto request, CancellationToken cancellationToken);
}

public sealed class AccountService(IAccountRepository repository) : IAccountService
{
    public async Task<UserDto> GetProfileAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        return new UserDto { UserId=user.UserId, Username=user.Username, FullName=user.FullName, Role=user.Role, IsActive=user.IsActive };
    }

    public async Task ChangePasswordAsync(int userId, ChangeOwnPasswordDto request, CancellationToken cancellationToken)
    {
        var user = await ActiveUserAsync(userId, cancellationToken);
        var next = request.NewPassword;
        if (string.IsNullOrWhiteSpace(next) || next.Length < 8 || Encoding.UTF8.GetByteCount(next) > 72 ||
            !next.Any(c => c is >= 'A' and <= 'Z') || !next.Any(c => c is >= 'a' and <= 'z') || !next.Any(char.IsDigit) || next.Any(char.IsControl))
            throw new ArgumentException("Mật khẩu mới cần ít nhất 8 ký tự, có chữ hoa, chữ thường, chữ số và không vượt quá 72 byte UTF-8.");
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || request.CurrentPassword.Length > 1024)
            throw new ArgumentException("Mật khẩu hiện tại không chính xác.");
        bool matches;
        try { matches = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash); }
        catch (BCrypt.Net.SaltParseException) { matches = false; }
        if (!matches) throw new ArgumentException("Mật khẩu hiện tại không chính xác.");
        if (request.CurrentPassword == next)
            throw new ArgumentException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        var hash = BCrypt.Net.BCrypt.HashPassword(next);
        if (!await repository.TryChangePasswordAsync(userId, user.PasswordHash, hash, cancellationToken))
            throw new AccountChangedException();
    }

    private async Task<User> ActiveUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = userId > 0 ? await repository.GetAsync(userId, cancellationToken) : null;
        if (user is null || !user.IsActive) throw new UnauthorizedAccessException();
        return user;
    }
}

public sealed class AccountChangedException : Exception;
