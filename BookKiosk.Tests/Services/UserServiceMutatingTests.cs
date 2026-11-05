using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using FluentAssertions;
using Moq;
using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace BookKiosk.Tests.Services;

public class UserServiceMutatingTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UserService _userService;

    public UserServiceMutatingTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userService = new UserService(_userRepoMock.Object, _unitOfWorkMock.Object);
    }

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_ValidInput_CreatesUserAndReturnsDto()
    {
        // Arrange
        var dto = new CreateUserDto
        {
            Username = "staff_test01",
            Password = "Password123",
            FullName = "Nguyễn Văn Thử Nghiệm",
            Role = UserRole.Staff
        };

        _userRepoMock.Setup(r => r.ExistsByUsernameAsync("staff_test01"))
            .ReturnsAsync(false);

        User? savedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u =>
            {
                u.UserId = 10;
                savedUser = u;
            })
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _userService.CreateUserAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(10);
        result.Username.Should().Be("staff_test01");
        result.FullName.Should().Be("Nguyễn Văn Thử Nghiệm");
        result.Role.Should().Be(UserRole.Staff);
        result.IsActive.Should().BeTrue();

        savedUser.Should().NotBeNull();
        savedUser!.PasswordHash.Should().NotBe(dto.Password);
        BCrypt.Net.BCrypt.Verify(dto.Password, savedUser.PasswordHash).Should().BeTrue();
    }

    [Theory]
    [InlineData("us")] // Quá ngắn (< 3)
    [InlineData("user@name")] // Ký tự đặc biệt không hợp lệ
    [InlineData("user name")] // Chứa khoảng trắng
    public async Task CreateUserAsync_InvalidUsername_ThrowsArgumentException(string username)
    {
        var dto = new CreateUserDto
        {
            Username = username,
            Password = "Password123",
            FullName = "Người Dùng Thử",
            Role = UserRole.Staff
        };

        var act = async () => await _userService.CreateUserAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*định dạng*");
    }

    [Theory]
    [InlineData("pass")] // Ngắn hơn 8 ký tự
    [InlineData("password123")] // Thiếu chữ hoa
    [InlineData("PASSWORD123")] // Thiếu chữ thường
    [InlineData("PasswordWord")] // Thiếu chữ số
    public async Task CreateUserAsync_WeakPassword_ThrowsArgumentException(string password)
    {
        var dto = new CreateUserDto
        {
            Username = "valid_user",
            Password = password,
            FullName = "Người Dùng Thử",
            Role = UserRole.Staff
        };

        var act = async () => await _userService.CreateUserAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tối thiểu 8 ký tự*");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        var dto = new CreateUserDto
        {
            Username = "existing_admin",
            Password = "Password123",
            FullName = "Người Dùng Trùng",
            Role = UserRole.Staff
        };

        _userRepoMock.Setup(r => r.ExistsByUsernameAsync("existing_admin"))
            .ReturnsAsync(true);

        var act = async () => await _userService.CreateUserAsync(dto);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã tồn tại*");
    }

    #endregion

    #region UpdateUserAsync Tests

    [Fact]
    public async Task UpdateUserAsync_ValidInput_UpdatesFullNameAndRole()
    {
        // Arrange
        var existingUser = new User
        {
            UserId = 5,
            Username = "staff01",
            FullName = "Tên Cũ",
            Role = UserRole.Staff,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(existingUser);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new UpdateUserDto
        {
            FullName = "Tên Mới Được Đổi",
            Role = UserRole.Manager
        };

        // Act
        var result = await _userService.UpdateUserAsync(5, dto);

        // Assert
        result.FullName.Should().Be("Tên Mới Được Đổi");
        result.Role.Should().Be(UserRole.Manager);
        existingUser.FullName.Should().Be("Tên Mới Được Đổi");
        existingUser.Role.Should().Be(UserRole.Manager);
        _userRepoMock.Verify(r => r.UpdateAsync(existingUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        var dto = new UpdateUserDto { FullName = "Họ Tên Hợp Lệ", Role = UserRole.Staff };
        var act = async () => await _userService.UpdateUserAsync(99, dto);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UpdateUserAsync_DemoteLastActiveAdmin_ThrowsInvalidOperationException()
    {
        // User hiện tại là Admin duy nhất
        var adminUser = new User
        {
            UserId = 1,
            Username = "superadmin",
            FullName = "Admin Tổng",
            Role = UserRole.Admin,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(adminUser);
        _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

        var dto = new UpdateUserDto
        {
            FullName = "Admin Tổng Sửa",
            Role = UserRole.Staff // Cố tình hạ quyền xuống Staff
        };

        var act = async () => await _userService.UpdateUserAsync(1, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất một tài khoản Quản trị viên*");
    }

    [Fact]
    public async Task UpdateUserAsync_DemoteAdminWhenMultipleAdminsExist_Succeeds()
    {
        var adminUser = new User
        {
            UserId = 2,
            Username = "admin02",
            FullName = "Admin Phụ",
            Role = UserRole.Admin,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(adminUser);
        _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(3); // Còn 2 admin khác

        var dto = new UpdateUserDto
        {
            FullName = "Admin Phụ Chuyển Vai Trò",
            Role = UserRole.Manager
        };

        var result = await _userService.UpdateUserAsync(2, dto);

        result.Role.Should().Be(UserRole.Manager);
        adminUser.Role.Should().Be(UserRole.Manager);
    }

    #endregion

    #region UpdateUserStatusAsync Tests

    [Fact]
    public async Task UpdateUserStatusAsync_SelfDeactivation_ThrowsArgumentException()
    {
        var user = new User
        {
            UserId = 1,
            Username = "admin",
            Role = UserRole.Admin,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        // currentAdminId = 1, target id = 1, isActive = false
        var act = async () => await _userService.UpdateUserStatusAsync(1, false, currentAdminId: 1);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tự vô hiệu hóa*");
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivateLastActiveAdmin_ThrowsInvalidOperationExceptionAndRollsBack()
    {
        var adminTarget = new User
        {
            UserId = 2,
            Username = "last_admin",
            Role = UserRole.Admin,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(adminTarget);
        _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1); // Chỉ còn 1 admin

        // currentAdminId = 1 (khác 2), isActive = false
        var act = async () => await _userService.UpdateUserStatusAsync(2, false, currentAdminId: 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ít nhất một tài khoản Quản trị viên*");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(IsolationLevel.Serializable), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivateStaff_Succeeds()
    {
        var staffUser = new User
        {
            UserId = 3,
            Username = "staff_target",
            Role = UserRole.Staff,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(staffUser);

        var result = await _userService.UpdateUserStatusAsync(3, false, currentAdminId: 1);

        result.IsActive.Should().BeFalse();
        staffUser.IsActive.Should().BeFalse();
        _userRepoMock.Verify(r => r.UpdateAsync(staffUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_ActivateUser_Succeeds()
    {
        var inactiveUser = new User
        {
            UserId = 4,
            Username = "inactive_staff",
            Role = UserRole.Staff,
            IsActive = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(inactiveUser);

        var result = await _userService.UpdateUserStatusAsync(4, true, currentAdminId: 1);

        result.IsActive.Should().BeTrue();
        inactiveUser.IsActive.Should().BeTrue();
        _userRepoMock.Verify(r => r.UpdateAsync(inactiveUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region ResetPasswordAsync Tests

    [Fact]
    public async Task ResetPasswordAsync_ValidPassword_HashesAndSaves()
    {
        var user = new User
        {
            UserId = 5,
            Username = "user_to_reset",
            PasswordHash = "OldHashValue",
            Role = UserRole.Staff,
            IsActive = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(user);

        await _userService.ResetPasswordAsync(5, "NewPassword999");

        user.PasswordHash.Should().NotBe("OldHashValue");
        BCrypt.Net.BCrypt.Verify("NewPassword999", user.PasswordHash).Should().BeTrue();
        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoDigitsInPassword")]
    public async Task ResetPasswordAsync_WeakPassword_ThrowsArgumentException(string newPassword)
    {
        var act = async () => await _userService.ResetPasswordAsync(5, newPassword);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tối thiểu 8 ký tự*");
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

        var act = async () => await _userService.ResetPasswordAsync(999, "SecurePassword123");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion
}

