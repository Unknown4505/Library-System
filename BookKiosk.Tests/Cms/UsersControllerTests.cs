using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace BookKiosk.Tests.Cms;

public class UserServiceAndContractTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;

    public UserServiceAndContractTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
    }

    #region Service Logic & Paging Clamp Tests

    [Theory]
    [InlineData(-1, -5, 1, 10)]      // Negative page and pageSize clamped to 1, 10
    [InlineData(0, 0, 1, 10)]        // Zero page and pageSize clamped to 1, 10
    [InlineData(2, 500, 2, 100)]     // Excessive pageSize clamped to 100
    [InlineData(3, 25, 3, 25)]       // Normal valid values kept
    public async Task UserService_GetUsersAsync_ClampsPagingCorrectly(
        int inputPage, int inputPageSize, int expectedPage, int expectedPageSize)
    {
        // Arrange
        int capturedPage = 0;
        int capturedPageSize = 0;

        _userRepositoryMock
            .Setup(r => r.GetUsersPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<bool?>()))
            .Callback<int, int, string?, UserRole?, bool?>((p, ps, kw, r, a) =>
            {
                capturedPage = p;
                capturedPageSize = ps;
            })
            .ReturnsAsync((Enumerable.Empty<User>(), 0));

        var service = new UserService(_userRepositoryMock.Object);

        var filter = new UserFilterDto
        {
            Page = inputPage,
            PageSize = inputPageSize
        };

        // Act
        var result = await service.GetUsersAsync(filter);

        // Assert
        capturedPage.Should().Be(expectedPage);
        capturedPageSize.Should().Be(expectedPageSize);
        result.Page.Should().Be(expectedPage);
        result.PageSize.Should().Be(expectedPageSize);
    }

    [Fact]
    public async Task UserService_GetUsersAsync_PassesFilterParametersToRepository()
    {
        // Arrange
        string? capturedKeyword = null;
        UserRole? capturedRole = null;
        bool? capturedIsActive = null;

        _userRepositoryMock
            .Setup(r => r.GetUsersPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<bool?>()))
            .Callback<int, int, string?, UserRole?, bool?>((p, ps, kw, r, a) =>
            {
                capturedKeyword = kw;
                capturedRole = r;
                capturedIsActive = a;
            })
            .ReturnsAsync((Enumerable.Empty<User>(), 0));

        var service = new UserService(_userRepositoryMock.Object);

        var filter = new UserFilterDto
        {
            Page = 1,
            PageSize = 10,
            Keyword = "admin",
            Role = UserRole.Admin,
            IsActive = true
        };

        // Act
        await service.GetUsersAsync(filter);

        // Assert
        capturedKeyword.Should().Be("admin");
        capturedRole.Should().Be(UserRole.Admin);
        capturedIsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UserService_GetUsersAsync_MapsEntitiesToDtos_Correctly()
    {
        // Arrange
        var entities = new List<User>
        {
            new() { UserId = 10, Username = "manager01", PasswordHash = "secret_hash_1", FullName = "Manager A", Role = UserRole.Manager, IsActive = true },
            new() { UserId = 11, Username = "staff02", PasswordHash = "secret_hash_2", FullName = "Staff B", Role = UserRole.Staff, IsActive = false }
        };

        _userRepositoryMock
            .Setup(r => r.GetUsersPagedAsync(1, 10, null, null, null))
            .ReturnsAsync((entities, 2));

        var service = new UserService(_userRepositoryMock.Object);

        // Act
        var result = await service.GetUsersAsync(new UserFilterDto());

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);

        var item1 = result.Items[0];
        item1.UserId.Should().Be(10);
        item1.Username.Should().Be("manager01");
        item1.FullName.Should().Be("Manager A");
        item1.Role.Should().Be(UserRole.Manager);
        item1.IsActive.Should().BeTrue();

        var item2 = result.Items[1];
        item2.UserId.Should().Be(11);
        item2.Role.Should().Be(UserRole.Staff);
        item2.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UserService_GetUsersAsync_WhenEmpty_ReturnsZeroCountAndEmptyList()
    {
        // Arrange
        _userRepositoryMock
            .Setup(r => r.GetUsersPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<UserRole?>(), It.IsAny<bool?>()))
            .ReturnsAsync((Enumerable.Empty<User>(), 0));

        var service = new UserService(_userRepositoryMock.Object);

        // Act
        var result = await service.GetUsersAsync(new UserFilterDto());

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task UserService_GetUserByIdAsync_WhenFound_MapsCorrectly()
    {
        // Arrange
        var entity = new User
        {
            UserId = 5,
            Username = "admin05",
            PasswordHash = "hash123",
            FullName = "Admin Five",
            Role = UserRole.Admin,
            IsActive = true
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(entity);

        var service = new UserService(_userRepositoryMock.Object);

        // Act
        var dto = await service.GetUserByIdAsync(5);

        // Assert
        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(5);
        dto.Username.Should().Be("admin05");
        dto.FullName.Should().Be("Admin Five");
        dto.Role.Should().Be(UserRole.Admin);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UserService_GetUserByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((User?)null);

        var service = new UserService(_userRepositoryMock.Object);

        // Act
        var dto = await service.GetUserByIdAsync(404);

        // Assert
        dto.Should().BeNull();
    }

    #endregion

    #region Security & Serialization Tests

    [Fact]
    public void UserDto_Serialization_ConvertsRoleToString_AndHidesPasswordHash()
    {
        // Arrange
        var dto = new UserDto
        {
            UserId = 1,
            Username = "admin_super",
            FullName = "Super Admin",
            Role = UserRole.Admin,
            IsActive = true
        };

        // Act: Serialize to JSON with standard options
        var json = JsonSerializer.Serialize(dto);

        // Assert 1: Role must be serialized as string "Admin" for CMS FE integration
        json.Should().Contain("\"Role\":\"Admin\"");

        // Assert 2: Must NEVER contain password or passwordHash
        json.ToLower().Should().NotContain("password");
        json.ToLower().Should().NotContain("hash");
    }

    [Theory]
    [InlineData(UserRole.Admin, "Admin")]
    [InlineData(UserRole.Staff, "Staff")]
    [InlineData(UserRole.Manager, "Manager")]
    public void UserDto_Serialization_SerializesAllRolesToStringCorrectly(UserRole role, string expectedString)
    {
        var dto = new UserDto { UserId = 1, Role = role };
        var json = JsonSerializer.Serialize(dto);
        json.Should().Contain($"\"Role\":\"{expectedString}\"");
    }

    [Fact]
    public void UserDto_Type_HasNoPasswordHashProperty()
    {
        // Reflection check: UserDto must not define PasswordHash property
        var prop = typeof(UserDto).GetProperty("PasswordHash", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        prop.Should().BeNull("UserDto must not have PasswordHash property");
    }

    [Fact]
    public void UserFilterDto_DefaultValues_AreCorrect()
    {
        var filter = new UserFilterDto();
        filter.Page.Should().Be(1);
        filter.PageSize.Should().Be(10);
        filter.Keyword.Should().BeNull();
        filter.Role.Should().BeNull();
        filter.IsActive.Should().BeNull();
    }

    #endregion
}
