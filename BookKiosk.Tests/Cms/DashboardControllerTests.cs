using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Dashboard;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class DashboardControllerTests
    {
        private readonly Mock<IReportApi> _reportApiMock;
        private readonly Mock<ICmsApiClient> _apiClientMock;
        private readonly Mock<ILogger<HomeController>> _loggerMock;

        public DashboardControllerTests()
        {
            _reportApiMock = new Mock<IReportApi>();
            _apiClientMock = new Mock<ICmsApiClient>();
            _loggerMock = new Mock<ILogger<HomeController>>();
        }

        private HomeController CreateController(string role = PermissionHelper.RoleAdmin)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            var httpContext = new DefaultHttpContext { User = user };

            return new HomeController(
                _reportApiMock.Object,
                _apiClientMock.Object,
                _loggerMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task Index_NonAdminUser_RedirectsToPosIndex(string role)
        {
            // Arrange
            var controller = CreateController(role);

            // Act
            var result = await controller.Index();

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Pos");
        }

        [Fact]
        public async Task Index_AdminUser_ReturnsViewWithPopulatedAdminDashboardViewModel()
        {
            // Arrange
            var monthlyData = Enumerable.Range(1, 12).Select(m => new MonthlyRevenueItemDto
            {
                Month = m,
                Revenue = m * 1_000_000m,
                TotalOrders = m * 10
            }).ToList();

            var revenueResponse = new RevenueReportResponseDto
            {
                Year = DateTime.UtcNow.Year,
                MonthlyData = monthlyData
            };

            var topBooks = new List<TopSellingBookDto>
            {
                new() { BookId = 1, Title = "Clean Code", TotalSold = 50, TotalRevenue = 15_000_000m },
                new() { BookId = 2, Title = "Refactoring", TotalSold = 30, TotalRevenue = 9_000_000m }
            };

            var stockItems = new List<StockItemDto>
            {
                new() { BookId = 10, Title = "Domain-Driven Design", AvailableStock = 2 },
                new() { BookId = 11, Title = "Pragmatic Programmer", AvailableStock = 4 }
            };

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(revenueResponse));

            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(10))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(topBooks));

            _apiClientMock.Setup(c => c.GetStockAsync(true, null))
                .ReturnsAsync(ApiResult<IEnumerable<StockItemDto>>.Ok(stockItems));

            var controller = CreateController(PermissionHelper.RoleAdmin);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<AdminDashboardViewModel>().Subject;

            model.SelectedYear.Should().Be(DateTime.UtcNow.Year);
            model.RevenueReport.Should().NotBeNull();
            model.RevenueReport!.MonthlyData.Should().HaveCount(12);
            model.TopBooks.Should().HaveCount(2);
            model.LowStockItems.Should().HaveCount(2);

            // Gate check: Summing in C# matches exactly
            decimal expectedSumRevenue = monthlyData.Sum(m => m.Revenue); // 1+2+...+12 * 1_000_000 = 78_000_000m
            int expectedSumOrders = monthlyData.Sum(m => m.TotalOrders); // 780
            model.TotalAnnualRevenue.Should().Be(expectedSumRevenue);
            model.TotalAnnualOrders.Should().Be(expectedSumOrders);
            model.LowStockCount.Should().Be(2);
        }

        [Fact]
        public void ViewModel_Sums12MonthsCorrectly_WithDecimalPrecision()
        {
            // Arrange
            var vm = new AdminDashboardViewModel
            {
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>
                    {
                        new() { Month = 1, Revenue = 1234567.89m, TotalOrders = 15 },
                        new() { Month = 2, Revenue = 9876543.21m, TotalOrders = 42 },
                        new() { Month = 3, Revenue = 0m, TotalOrders = 0 },
                        new() { Month = 4, Revenue = 500000.00m, TotalOrders = 5 }
                    }
                }
            };

            // Act & Assert
            // 1234567.89 + 9876543.21 + 0 + 500000.00 = 11611111.10m
            vm.TotalAnnualRevenue.Should().Be(11611111.10m);
            vm.TotalAnnualOrders.Should().Be(62);
        }

        [Fact]
        public void GetRevenueChartData_HasAdminAuthorizeAttribute()
        {
            var method = typeof(HomeController).GetMethod("GetRevenueChartData");
            method.Should().NotBeNull();

            var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            authAttr.Should().NotBeNull();
            authAttr!.Roles.Should().Be("Admin");
        }

        [Fact]
        public void GetTopBooksJson_HasAdminAuthorizeAttribute()
        {
            var method = typeof(HomeController).GetMethod("GetTopBooksJson");
            method.Should().NotBeNull();

            var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
            authAttr.Should().NotBeNull();
            authAttr!.Roles.Should().Be("Admin");
        }

        [Fact]
        public async Task GetRevenueChartData_Admin_ReturnsJsonDataWithTotals()
        {
            // Arrange
            var monthlyData = Enumerable.Range(1, 12).Select(m => new MonthlyRevenueItemDto
            {
                Month = m,
                Revenue = m * 500_000m,
                TotalOrders = m * 5
            }).ToList();

            var revenueResponse = new RevenueReportResponseDto
            {
                Year = 2026,
                MonthlyData = monthlyData
            };

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(2026))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(revenueResponse));

            var controller = CreateController(PermissionHelper.RoleAdmin);

            // Act
            var result = await controller.GetRevenueChartData(2026);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            jsonResult.Value.Should().NotBeNull();
        }
    }
}
