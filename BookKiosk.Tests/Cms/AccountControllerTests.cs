using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Models.Auth;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class AccountControllerTests
    {
        private readonly Mock<IAuthenticationService> _authServiceMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IUrlHelper> _urlHelperMock;
        private readonly Mock<ICmsApiClient> _apiClientMock;
        private readonly Mock<ILogger<AccountController>> _loggerMock;

        public AccountControllerTests()
        {
            _authServiceMock = new Mock<IAuthenticationService>();
            _authServiceMock
                .Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            _serviceProviderMock = new Mock<IServiceProvider>();
            _serviceProviderMock
                .Setup(s => s.GetService(typeof(IAuthenticationService)))
                .Returns(_authServiceMock.Object);

            _urlHelperMock = new Mock<IUrlHelper>();
            _urlHelperMock.Setup(u => u.IsLocalUrl(It.IsAny<string?>()))
                .Returns<string?>(url => !string.IsNullOrEmpty(url) && url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\"));

            _apiClientMock = new Mock<ICmsApiClient>();
            _loggerMock = new Mock<ILogger<AccountController>>();
        }

        private AccountController CreateController()
        {
            var controller = new AccountController(_apiClientMock.Object, _loggerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { RequestServices = _serviceProviderMock.Object }
                },
                Url = _urlHelperMock.Object
            };
            return controller;
        }

        [Fact]
        public async Task Login_AdminRole_RedirectsToHomeIndex()
        {
            // Arrange
            _apiClientMock.Setup(c => c.LoginAsync(It.IsAny<LoginRequestModel>()))
                .ReturnsAsync(ApiResult<LoginResponseData>.Ok(new LoginResponseData
                {
                    FullName = "Administrator",
                    Role = "Admin",
                    AccessToken = "mock-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Login(new LoginRequestModel { Username = "admin", Password = "123" }, null);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Login_StaffRole_RedirectsToPosIndex()
        {
            // Arrange
            _apiClientMock.Setup(c => c.LoginAsync(It.IsAny<LoginRequestModel>()))
                .ReturnsAsync(ApiResult<LoginResponseData>.Ok(new LoginResponseData
                {
                    FullName = "Cashier Staff",
                    Role = "Staff",
                    AccessToken = "mock-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Login(new LoginRequestModel { Username = "staff", Password = "123" }, null);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Pos");
        }

        [Fact]
        public async Task Login_ManagerRole_RedirectsToStaffWorkspace()
        {
            // Arrange
            _apiClientMock.Setup(c => c.LoginAsync(It.IsAny<LoginRequestModel>()))
                .ReturnsAsync(ApiResult<LoginResponseData>.Ok(new LoginResponseData
                {
                    FullName = "Store Manager",
                    Role = "Manager",
                    AccessToken = "mock-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Login(new LoginRequestModel { Username = "manager", Password = "123" }, null);

            // Assert
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("StaffWorkspace");
            redirect.ControllerName.Should().Be("Home");
        }

        [Theory]
        [InlineData("https://malicious.com/steal-cookie")]
        [InlineData("//malicious.com")]
        [InlineData("/\\malicious.com")]
        public async Task Login_ExternalReturnUrl_BlockedAndFallsBackToDefault(string externalUrl)
        {
            // Arrange
            _apiClientMock.Setup(c => c.LoginAsync(It.IsAny<LoginRequestModel>()))
                .ReturnsAsync(ApiResult<LoginResponseData>.Ok(new LoginResponseData
                {
                    FullName = "Administrator",
                    Role = "Admin",
                    AccessToken = "mock-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Login(new LoginRequestModel { Username = "admin", Password = "123" }, externalUrl);

            // Assert: Không redirect ra domain ngoài, mà fallback về Home/Index cho Admin
            result.Should().NotBeOfType<RedirectResult>();
            var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Login_ValidLocalReturnUrl_RedirectsToLocalUrl()
        {
            // Arrange
            _apiClientMock.Setup(c => c.LoginAsync(It.IsAny<LoginRequestModel>()))
                .ReturnsAsync(ApiResult<LoginResponseData>.Ok(new LoginResponseData
                {
                    FullName = "Administrator",
                    Role = "Admin",
                    AccessToken = "mock-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Login(new LoginRequestModel { Username = "admin", Password = "123" }, "/Orders/Index");

            // Assert
            var redirect = result.Should().BeOfType<RedirectResult>().Subject;
            redirect.Url.Should().Be("/Orders/Index");
        }
    }
}
