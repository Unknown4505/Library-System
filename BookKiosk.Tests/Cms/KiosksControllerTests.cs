using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.ViewModels.Kiosks;
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
    public class KiosksControllerTests
    {
        private readonly Mock<ILogger<KiosksController>> _loggerMock;

        public KiosksControllerTests()
        {
            _loggerMock = new Mock<ILogger<KiosksController>>();
        }

        private KiosksController CreateController(string role = PermissionHelper.RoleAdmin, IConfiguration? config = null)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "TestAdmin"),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            var httpContext = new DefaultHttpContext { User = user };

            return new KiosksController(
                config ?? new ConfigurationBuilder().AddInMemoryCollection().Build(),
                _loggerMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        [Fact]
        public void KiosksController_ShouldHaveAuthorizeAttribute_WithAdminRoleOnly()
        {
            // Arrange & Act
            var authAttribute = typeof(KiosksController).GetCustomAttribute<AuthorizeAttribute>();

            // Assert
            authAttribute.Should().NotBeNull("KiosksController phải có [Authorize]");
            authAttribute!.Roles.Should().Be("Admin", "KiosksController chỉ cho phép duy nhất Role Admin");
        }

        [Fact]
        public void KiosksController_ShouldHaveRouteAttribute_Kiosks()
        {
            // Arrange & Act
            var routeAttribute = typeof(KiosksController).GetCustomAttribute<RouteAttribute>();

            // Assert
            routeAttribute.Should().NotBeNull("KiosksController phải có [Route]");
            routeAttribute!.Template.Should().Be("Kiosks", "Route của KiosksController phải là /Kiosks");
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
            var model = viewResult.Model.Should().BeOfType<KioskMonitoringViewModel>().Subject;

            model.UseMockApi.Should().BeFalse();
            model.IsBackendAvailable.Should().BeFalse("Backend chưa có endpoint nên IsBackendAvailable phải là false");
            model.Kiosks.Should().BeEmpty("Danh sách thiết bị phải để trống, không hiển thị dữ liệu giả");
            model.TotalKiosks.Should().Be(0);
            model.OnlineCount.Should().Be(0);
            model.WarningCount.Should().Be(0);
            model.OfflineCount.Should().Be(0);
            model.StatusMessage.Should().Contain("Docs 04 §3.7");
        }

        [Fact]
        public void Index_WhenUseMockApiIsTrue_PopulatesMockLayoutDataForDevelopmentTesting()
        {
            // Arrange: Dev Mock mode (UseMockApi = true)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cms:UseMockApi"] = "true"
                })
                .Build();

            var controller = CreateController(config: config);

            // Act
            var result = controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<KioskMonitoringViewModel>().Subject;

            model.UseMockApi.Should().BeTrue();
            model.IsBackendAvailable.Should().BeTrue();
            model.Kiosks.Should().HaveCount(3);
            model.TotalKiosks.Should().Be(3);
            model.OnlineCount.Should().Be(2);
            model.WarningCount.Should().Be(1);
            model.OfflineCount.Should().Be(0);
        }
    }

    public class KiosksIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public KiosksIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task GetKiosks_WhenCalledByAdmin_Returns200OkAndRendersKiosksPage()
        {
            // Arrange
            var client = CreateClientForRole("Admin");

            // Act
            var response = await client.GetAsync("/Kiosks");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            html.Should().Contain("Giám sát trạm Kiosk");
            html.Should().Contain("Tỷ lệ trạng thái Kiosk");
            html.Should().Contain("Danh sách máy trạm Kiosk bán lẻ");
        }

        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task GetKiosks_WhenCalledByStaffOrManager_IsForbiddenOrRedirectsToAccessDenied(string role)
        {
            // Arrange
            var client = CreateClientForRole(role);

            // Act
            var response = await client.GetAsync("/Kiosks");

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

