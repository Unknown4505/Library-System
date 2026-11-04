using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.ViewModels.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class UsersControllerTests
    {
        private readonly Mock<ILogger<UsersController>> _loggerMock;

        public UsersControllerTests()
        {
            _loggerMock = new Mock<ILogger<UsersController>>();
        }

        private UsersController CreateController(string role = PermissionHelper.RoleAdmin, IConfiguration? config = null)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "TestAdmin"),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            var httpContext = new DefaultHttpContext { User = user };

            return new UsersController(
                config ?? new ConfigurationBuilder().AddInMemoryCollection().Build(),
                _loggerMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        [Fact]
        public void UsersController_ShouldHaveAuthorizeAttribute_WithAdminRoleOnly()
        {
            // Arrange & Act
            var authAttribute = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();

            // Assert
            authAttribute.Should().NotBeNull("UsersController phải có [Authorize]");
            authAttribute!.Roles.Should().Be("Admin", "UsersController chỉ cho phép duy nhất Role Admin");
        }

        [Fact]
        public void UsersController_ShouldHaveRouteAttribute_Users()
        {
            // Arrange & Act
            var routeAttribute = typeof(UsersController).GetCustomAttribute<RouteAttribute>();

            // Assert
            routeAttribute.Should().NotBeNull("UsersController phải có [Route]");
            routeAttribute!.Template.Should().Be("Users", "Route của UsersController phải là /Users");
        }

        [Fact]
        public void Index_WhenUseMockApiIsFalse_LeavesMissingPartsEmptyAccordingToHonestView()
        {
            // Arrange: Production / Real Backend mode (UseMockApi = false)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cms:UseMockApi"] = "false"
                })
                .Build();

            var controller = CreateController(config: config);

            // Act
            var result = controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<UserManagementViewModel>().Subject;

            model.UseMockApi.Should().BeFalse();
            model.IsBackendAvailable.Should().BeFalse("Backend chưa có endpoint nên IsBackendAvailable phải là false");
            model.Users.Should().BeEmpty("Danh sách tài khoản phải để trống khi Backend chưa có endpoint");
            model.TotalUsers.Should().Be(0);
            model.AdminCount.Should().Be(0);
            model.ManagerCount.Should().Be(0);
            model.StaffCount.Should().Be(0);
            model.StatusMessage.Should().Contain("Docs 04 §3.8");
        }

        [Fact]
        public void Index_WhenUseMockApiIsTrue_PopulatesMockUsersAndSupportsFiltering()
        {
            // Arrange: Dev Mock mode (UseMockApi = true)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cms:UseMockApi"] = "true"
                })
                .Build();

            var controller = CreateController(config: config);

            // Act: Lấy tất cả
            var resultAll = controller.Index();
            var modelAll = resultAll.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<UserManagementViewModel>().Subject;

            // Assert
            modelAll.UseMockApi.Should().BeTrue();
            modelAll.IsBackendAvailable.Should().BeTrue();
            modelAll.Users.Should().HaveCount(3);
            modelAll.TotalUsers.Should().Be(3);
            modelAll.AdminCount.Should().Be(1);
            modelAll.ManagerCount.Should().Be(1);
            modelAll.StaffCount.Should().Be(1);

            // Act: Lọc theo Role Staff
            var resultStaff = controller.Index(role: "Staff");
            var modelStaff = resultStaff.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<UserManagementViewModel>().Subject;
            modelStaff.Users.Should().HaveCount(1);
            modelStaff.Users[0].Role.Should().Be("Staff");

            // Act: Lọc theo Keyword "admin"
            var resultSearch = controller.Index(search: "admin");
            var modelSearch = resultSearch.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<UserManagementViewModel>().Subject;
            modelSearch.Users.Should().HaveCount(1);
            modelSearch.Users[0].Username.Should().Be("admin");
        }
    }

    public class UsersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public UsersIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private System.Net.Http.HttpClient CreateClientForRole(string? role = null)
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(role))
            {
                client.DefaultRequestHeaders.Add("X-Test-Roles", role);
            }

            return client;
        }

        [Fact]
        public async Task GetUsers_WhenCalledByAdmin_Returns200OkAndRendersUsersPage()
        {
            // Arrange
            var client = CreateClientForRole("Admin");

            // Act
            var response = await client.GetAsync("/Users");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            html.Should().Contain("Tài khoản người dùng");
            html.Should().Contain("Danh sách tài khoản người dùng");
            html.Should().Contain("Quản lý truy cập");
        }

        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task GetUsers_WhenCalledByStaffOrManager_IsForbiddenOrRedirectsToAccessDenied(string role)
        {
            // Arrange
            var client = CreateClientForRole(role);

            // Act
            var response = await client.GetAsync("/Users");

            // Assert: Cookie Auth redirects to AccessDenied (302) or returns 403 Forbidden
            if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
            {
                response.Headers.Location.Should().NotBeNull();
                response.Headers.Location!.ToString().Should().Contain("AccessDenied");
            }
            else
            {
                response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
            }
        }
    }
}
