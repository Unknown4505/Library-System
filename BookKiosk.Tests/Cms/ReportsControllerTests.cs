using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Reports;
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
    public class ReportsControllerTests
    {
        private readonly Mock<IReportApi> _reportApiMock;
        private readonly Mock<ILogger<ReportsController>> _loggerMock;
        private readonly IConfiguration _configuration;

        public ReportsControllerTests()
        {
            _reportApiMock = new Mock<IReportApi>();
            _loggerMock = new Mock<ILogger<ReportsController>>();
            _configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        }

        private ReportsController CreateController(string? role = PermissionHelper.RoleAdmin, IConfiguration? config = null, bool isAuthenticated = true)
        {
            ClaimsPrincipal user;
            if (isAuthenticated && !string.IsNullOrEmpty(role))
            {
                user = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "TestAdmin"),
                    new Claim(ClaimTypes.Role, role)
                }, "TestAuth"));
            }
            else
            {
                user = new ClaimsPrincipal(new ClaimsIdentity());
            }

            var httpContext = new DefaultHttpContext { User = user };

            return new ReportsController(
                _reportApiMock.Object,
                config ?? _configuration,
                _loggerMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        // ========================================================
        // 1. RBAC & ATTRIBUTES VERIFICATION
        // ========================================================
        [Fact]
        public void ReportsController_ShouldHaveAuthorizeAttribute_WithAdminRoleOnly()
        {
            // Arrange & Act
            var authAttribute = typeof(ReportsController).GetCustomAttribute<AuthorizeAttribute>();

            // Assert
            authAttribute.Should().NotBeNull("ReportsController phải có [Authorize]");
            authAttribute!.Roles.Should().Be("Admin", "ReportsController chỉ cho phép duy nhất Role Admin");
        }

        [Fact]
        public void ReportsController_ShouldHaveRouteAttribute_Reports()
        {
            // Arrange & Act
            var routeAttribute = typeof(ReportsController).GetCustomAttribute<RouteAttribute>();

            // Assert
            routeAttribute.Should().NotBeNull("ReportsController phải có [Route]");
            routeAttribute!.Template.Should().Be("Reports", "Route của ReportsController phải là /Reports");
        }

        // ========================================================
        // 2. YEAR SELECTION & DEFAULT VALUES
        // ========================================================
        [Fact]
        public async Task Index_WithoutYearParameter_DefaultsToCurrentVietnamYear()
        {
            // Arrange
            int expectedYear = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).Year;

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(expectedYear))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto { Year = expectedYear }));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index(null);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.SelectedYear.Should().Be(expectedYear);
            _reportApiMock.Verify(r => r.GetRevenueByMonthAsync(expectedYear), Times.Once);
        }

        [Fact]
        public async Task Index_WithSpecificYear_PassesRequestedYearToReportApi()
        {
            // Arrange
            int requestedYear = 2024;

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(requestedYear))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto { Year = requestedYear }));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index(requestedYear);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.SelectedYear.Should().Be(requestedYear);
            _reportApiMock.Verify(r => r.GetRevenueByMonthAsync(requestedYear), Times.Once);
        }

        [Theory]
        [InlineData(1999)]
        [InlineData(2101)]
        [InlineData(-5)]
        public async Task Index_WithYearOutOfRange_SetsErrorMessageAndDefaultsToCurrentYear(int outOfRangeYear)
        {
            // Arrange
            int expectedCurrentYear = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).Year;

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(expectedCurrentYear))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto { Year = expectedCurrentYear }));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index(outOfRangeYear);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.SelectedYear.Should().Be(expectedCurrentYear);
            model.ErrorMessage.Should().NotBeNullOrEmpty();
            model.ErrorMessage.Should().Contain("không nằm trong khoảng hợp lệ");
            _reportApiMock.Verify(r => r.GetRevenueByMonthAsync(expectedCurrentYear), Times.Once);
        }

        [Fact]
        public async Task Index_BuildsAvailableYears_With5RecentYears()
        {
            // Arrange
            int currentYear = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).Year;

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto()));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.AvailableYears.Should().Contain(new[]
            {
                currentYear,
                currentYear - 1,
                currentYear - 2,
                currentYear - 3,
                currentYear - 4
            });
        }

        // ========================================================
        // 3. 12-MONTH NORMALIZATION & AVERAGE CALCULATIONS
        // ========================================================
        [Fact]
        public void MonthlyRows_WhenDataHasMissingMonths_NormalizesTo12MonthsWithZeros()
        {
            // Arrange
            var vm = new ReportsIndexViewModel
            {
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>
                    {
                        new() { Month = 3, Revenue = 30_000_000m, TotalOrders = 100 },
                        new() { Month = 7, Revenue = 70_000_000m, TotalOrders = 200 }
                    }
                }
            };

            // Act
            var rows = vm.MonthlyRows;

            // Assert
            rows.Should().HaveCount(12, "Phải luôn đủ 12 tháng từ tháng 1 đến tháng 12");

            // Tháng 1 thiếu -> 0
            rows[0].Month.Should().Be(1);
            rows[0].Revenue.Should().Be(0m);
            rows[0].TotalOrders.Should().Be(0);
            rows[0].AverageOrderValue.Should().Be(0m);

            // Tháng 3 có dữ liệu
            rows[2].Month.Should().Be(3);
            rows[2].Revenue.Should().Be(30_000_000m);
            rows[2].TotalOrders.Should().Be(100);
            rows[2].AverageOrderValue.Should().Be(300_000m);

            // Tháng 7 có dữ liệu
            rows[6].Month.Should().Be(7);
            rows[6].Revenue.Should().Be(70_000_000m);
            rows[6].TotalOrders.Should().Be(200);
            rows[6].AverageOrderValue.Should().Be(350_000m);

            // Tháng 12 thiếu -> 0
            rows[11].Month.Should().Be(12);
            rows[11].Revenue.Should().Be(0m);
            rows[11].TotalOrders.Should().Be(0);
            rows[11].AverageOrderValue.Should().Be(0m);
        }

        [Fact]
        public void MonthlyRows_WhenDataContainsDuplicateMonths_RejectsContractAndMarksUnavailable()
        {
            // Arrange - REVENUE-MONTHLY-01 Option A: Backend trả về danh sách có tháng bị trùng lặp (ví dụ 2 bản ghi tháng 3, 2 bản ghi tháng 8)
            var vm = new ReportsIndexViewModel
            {
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>
                    {
                        new() { Month = 3, Revenue = 15_000_000m, TotalOrders = 50 },
                        new() { Month = 3, Revenue = 25_000_000m, TotalOrders = 70 }, // Trùng tháng 3
                        new() { Month = 8, Revenue = 10_000_000m, TotalOrders = 20 },
                        new() { Month = 8, Revenue = 30_000_000m, TotalOrders = 40 }  // Trùng tháng 8
                    }
                }
            };

            // Act - Không được GroupBy+Sum ngầm, phải từ chối payload
            var rows = vm.MonthlyRows;
            var isUnavailable = vm.IsRevenueUnavailable;
            var totalRevenue = vm.TotalAnnualRevenue;
            var totalOrders = vm.TotalAnnualOrders;

            // Assert
            isUnavailable.Should().BeTrue("Dữ liệu trùng tháng vi phạm hợp đồng backend và phải bị từ chối");
            rows.Should().BeEmpty("Khi payload không hợp lệ, không trả về dòng dữ liệu nào");
            totalRevenue.Should().Be(0m);
            totalOrders.Should().Be(0);
        }

        [Fact]
        public void ChartDataAndTableRows_WhenDataIsValidAndSparse_AreIdenticalAndConsistent()
        {
            // Arrange - REPORT-CONSISTENCY-01: Dữ liệu hợp lệ chuẩn hóa 12 tháng
            var vm = new ReportsIndexViewModel
            {
                SelectedYear = 2026,
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>
                    {
                        new() { Month = 3, Revenue = 25_000_000m, TotalOrders = 70 },
                        new() { Month = 8, Revenue = 30_000_000m, TotalOrders = 40 }
                    }
                }
            };

            // Act - Dữ liệu cấp cho Canvas Chart.js (data-revenue) bắt nguồn từ MonthlyRows
            var chartSourceRows = vm.MonthlyRows;
            var jsonChartData = System.Text.Json.JsonSerializer.Serialize(chartSourceRows);
            var deserializedChartData = System.Text.Json.JsonSerializer.Deserialize<List<MonthlyReportRow>>(jsonChartData)!;

            // Assert: 12 tháng của Chart Data và Table Rows phải trùng khớp nhau 100%
            deserializedChartData.Should().HaveCount(12);
            chartSourceRows.Should().HaveCount(12);

            for (int i = 0; i < 12; i++)
            {
                deserializedChartData[i].Month.Should().Be(chartSourceRows[i].Month);
                deserializedChartData[i].Revenue.Should().Be(chartSourceRows[i].Revenue);
                deserializedChartData[i].TotalOrders.Should().Be(chartSourceRows[i].TotalOrders);
                deserializedChartData[i].AverageOrderValue.Should().Be(chartSourceRows[i].AverageOrderValue);
            }

            // Tháng 3 trên biểu đồ và bảng đều là 25,000,000 / 70 đơn
            deserializedChartData[2].Revenue.Should().Be(25_000_000m);
            deserializedChartData[2].TotalOrders.Should().Be(70);

            // Tháng 8 trên biểu đồ và bảng đều là 30,000,000 / 40 đơn
            deserializedChartData[7].Revenue.Should().Be(30_000_000m);
            deserializedChartData[7].TotalOrders.Should().Be(40);

            // Tổng doanh thu và đơn hàng của Chart Data khớp 100% với KPI cards
            deserializedChartData.Sum(x => x.Revenue).Should().Be(vm.TotalAnnualRevenue);
            deserializedChartData.Sum(x => x.TotalOrders).Should().Be(vm.TotalAnnualOrders);
            vm.AnnualAverageOrderValue.Should().Be(Math.Round(vm.TotalAnnualRevenue / vm.TotalAnnualOrders));
        }

        [Fact]
        public void AnnualTotals_CalculatesSumAndAverageCorrectly_AndGuardsDivideByZero()
        {
            // Arrange
            var vm = new ReportsIndexViewModel
            {
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>
                    {
                        new() { Month = 1, Revenue = 10_000_000m, TotalOrders = 50 },
                        new() { Month = 2, Revenue = 20_000_000m, TotalOrders = 50 }
                    }
                }
            };

            // Act & Assert
            vm.TotalAnnualRevenue.Should().Be(30_000_000m);
            vm.TotalAnnualOrders.Should().Be(100);
            vm.AnnualAverageOrderValue.Should().Be(300_000m); // 30,000,000 / 100

            // Scenario TotalOrders = 0: no divide by zero
            var emptyVm = new ReportsIndexViewModel
            {
                RevenueReport = new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto>()
                }
            };
            emptyVm.TotalAnnualRevenue.Should().Be(0m);
            emptyVm.TotalAnnualOrders.Should().Be(0);
            emptyVm.AnnualAverageOrderValue.Should().Be(0m);
        }

        // ========================================================
        // 4. TOP BOOKS & IMAGE URL RESOLUTION
        // ========================================================
        [Fact]
        public void GetSafeImageUrl_WhenNullOrEmpty_ReturnsPlaceholder()
        {
            // Arrange
            var vm = new ReportsIndexViewModel { ApiBaseUrl = "https://backend.test" };

            // Act & Assert
            vm.GetSafeImageUrl(null).Should().Contain("placehold.co");
            vm.GetSafeImageUrl("").Should().Contain("placehold.co");
            vm.GetSafeImageUrl("   ").Should().Contain("placehold.co");
        }

        [Fact]
        public void GetSafeImageUrl_WhenRelativePath_PrependsApiBaseUrl()
        {
            // Arrange
            var vm = new ReportsIndexViewModel { ApiBaseUrl = "https://backend.test:7111/" };

            // Act
            var url = vm.GetSafeImageUrl("/uploads/books/book1.jpg");

            // Assert
            url.Should().Be("https://backend.test:7111/uploads/books/book1.jpg");
        }

        [Fact]
        public void GetSafeImageUrl_WhenAbsoluteUrl_ReturnsAsIs()
        {
            // Arrange
            var vm = new ReportsIndexViewModel { ApiBaseUrl = "https://backend.test:7111" };

            // Act
            var url = vm.GetSafeImageUrl("https://cdn.example.com/cover.png");

            // Assert
            url.Should().Be("https://cdn.example.com/cover.png");
        }

        // ========================================================
        // 5. ERROR RESILIENCE (401, 403, 404, 503, Exceptions)
        // ========================================================
        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(503)]
        public async Task Index_WhenRevenueApiReturnsHttpError_SetsUnavailableFlagWithoutThrowing(int statusCode)
        {
            // Arrange
            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Fail("Lỗi kết nối", statusCode: statusCode));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.IsRevenueUnavailable.Should().BeTrue();
            model.StatusCode.Should().Be(statusCode);
        }

        [Fact]
        public async Task Index_WhenRevenueApiThrowsException_SetsUnavailableFlagWithoutCrashing()
        {
            // Arrange
            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("Connection refused"));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.IsRevenueUnavailable.Should().BeTrue();
        }

        [Fact]
        public async Task Index_WhenTopBooksApiFails_SetsTopBooksUnavailableWithoutAffectingRevenue()
        {
            // Arrange
            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto
                {
                    Year = 2026,
                    MonthlyData = new List<MonthlyRevenueItemDto> { new() { Month = 1, Revenue = 1000m, TotalOrders = 1 } }
                }));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Fail("Backend 503", statusCode: 503));

            var controller = CreateController();

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.IsRevenueUnavailable.Should().BeFalse();
            model.IsTopBooksUnavailable.Should().BeTrue();
            model.TopBooks.Should().BeEmpty();
        }

        // ========================================================
        // 6. USE MOCK API FLAG
        // ========================================================
        [Fact]
        public async Task Index_WhenUseMockApiConfigured_PopulatesUseMockApiInViewModel()
        {
            // Arrange
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cms:UseMockApi"] = "true"
                })
                .Build();

            _reportApiMock.Setup(r => r.GetRevenueByMonthAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<RevenueReportResponseDto>.Ok(new RevenueReportResponseDto()));
            _reportApiMock.Setup(r => r.GetTopSellingBooksAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(new List<TopSellingBookDto>()));

            var controller = CreateController(config: config);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<ReportsIndexViewModel>().Subject;
            model.UseMockApi.Should().BeTrue();
        }
    }

    public class ReportsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public ReportsIntegrationTests(CustomWebApplicationFactory factory)
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
        public async Task GetReports_WhenCalledByAdmin_Returns200OkAndRendersReportsPage()
        {
            // Arrange
            var client = CreateClientForRole("Admin");

            // Act
            var response = await client.GetAsync("/Reports");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            html.Should().Contain("Báo cáo doanh thu");
            html.Should().Contain("Chọn năm");
            html.Should().Contain("Top 10 Sách Bán Chạy Nhất");
        }

        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task GetReports_WhenCalledByStaffOrManager_IsForbiddenOrRedirectsToAccessDenied(string role)
        {
            // Arrange
            var client = CreateClientForRole(role);

            // Act
            var response = await client.GetAsync("/Reports");

            // Assert: ASP.NET Core Cookie Auth redirects to AccessDenied (302) or returns 403 Forbidden
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
