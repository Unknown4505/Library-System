using BookKiosk.Application.DTOs.Auth;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BookKiosk.Tests.Backend;

public class UserCredentialLifecycleTests
{
    [Fact]
    public async Task Reset_password_rejects_old_credential_and_accepts_new_one_without_exposing_hash()
    {
        var user = new User { UserId = 33, Username = "qa_admin", FullName = "QA Admin", Role = UserRole.Admin,
            IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword1") };
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.GetByIdAsync(33)).ReturnsAsync(user);
        repository.Setup(x => x.GetByUsernameAsync("qa_admin")).ReturnsAsync(user);
        var work = new Mock<IUnitOfWork>();
        await new UserService(repository.Object, work.Object).ResetPasswordAsync(33, "NewPassword2");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Jwt:Key"] = "QA_ONLY_USER_LIFECYCLE_TEST_SIGNING_KEY_123456789" }).Build();
        var auth = new AuthService(repository.Object, config);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "OldPassword1" }));
        var response = await auth.LoginAsync(new LoginRequestDto { Username = user.Username, Password = "NewPassword2" });
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.DoesNotContain(user.PasswordHash, System.Text.Json.JsonSerializer.Serialize(response));
        work.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Inactive_user_cannot_login_even_with_valid_password()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.GetByUsernameAsync("qa_user")).ReturnsAsync(new User
        { Username = "qa_user", IsActive = false, PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword1") });
        var auth = new AuthService(repository.Object, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auth.LoginAsync(new LoginRequestDto { Username = "qa_user", Password = "ValidPassword1" }));
    }
}
