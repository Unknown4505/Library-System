using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class DashboardIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public DashboardIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientForRole(string? role = null)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            if (!string.IsNullOrEmpty(role))
            {
                client.DefaultRequestHeaders.Add("X-Test-Roles", role);
            }

            return client;
        }

        // ==========================================
        // 1. TEST HTTP 403 CHO STAFF & MANAGER
        // ==========================================
        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task GetRevenueChartData_WhenCalledByStaffOrManager_Returns403Forbidden(string role)
        {
            // Arrange
            var client = CreateClientForRole(role);

            // Act
            var response = await client.GetAsync("/Home/GetRevenueChartData");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Theory]
        [InlineData("Staff")]
        [InlineData("Manager")]
        public async Task GetTopBooksJson_WhenCalledByStaffOrManager_Returns403Forbidden(string role)
        {
            // Arrange
            var client = CreateClientForRole(role);

            // Act
            var response = await client.GetAsync("/Home/GetTopBooksJson?top=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetRevenueChartData_WhenCalledByAdmin_Returns200Ok()
        {
            // Arrange
            var client = CreateClientForRole("Admin");

            // Act
            var response = await client.GetAsync("/Home/GetRevenueChartData");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task GetTopBooksJson_WhenCalledByAdmin_Returns200Ok()
        {
            // Arrange
            var client = CreateClientForRole("Admin");

            // Act
            var response = await client.GetAsync("/Home/GetTopBooksJson?top=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task StaffWorkspace_WhenCalledByManager_Returns200OkAndRendersWorkspace()
        {
            // Arrange
            var client = CreateClientForRole("Manager");

            // Act
            var response = await client.GetAsync("/Home/StaffWorkspace");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Bàn làm việc nhân viên");
        }

        // ==========================================
        // 2. TEST CÔ LẬP LỖI WIDGET TRÊN GIAO DIỆN RAZOR
        // ==========================================
        [Fact]
        public async Task StaffWorkspace_WhenOrdersApiFails_Returns200AndRendersStockItemsWhileShowingOrdersError()
        {
            // Arrange: Tạo client với OrderApi giả lập lỗi exception, nhưng CmsApiClient giả lập tồn kho thành công
            var mockOrderApi = new Mock<IOrderApi>();
            mockOrderApi.Setup(x => x.GetOrdersAsync(It.IsAny<OrderFilterDto>()))
                .ThrowsAsync(new HttpRequestException("Simulated Orders Service Failure"));

            var mockApiClient = new Mock<ICmsApiClient>();
            var stockItems = new System.Collections.Generic.List<StockItemDto>
            {
                new StockItemDto
                {
                    BookId = 1,
                    Title = "Đất rừng phương Nam",
                    Barcode = "9786042123456",
                    CategoryName = "Văn học",
                    AvailableStock = 2
                }
            };
            mockApiClient.Setup(x => x.GetStockAsync(true, null))
                .ReturnsAsync(ApiResult<System.Collections.Generic.IEnumerable<StockItemDto>>.Ok(stockItems));

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(mockOrderApi.Object);
                    services.AddSingleton(mockApiClient.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act: Gọi StaffWorkspace với vai trò Staff
            client.DefaultRequestHeaders.Add("X-Test-Roles", "Staff");
            var response = await client.GetAsync("/Home/StaffWorkspace");
            var html = await response.Content.ReadAsStringAsync();

            // Assert: Trang vẫn render 200 OK
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Widget đơn hàng: hiển thị cảnh báo cô lập lỗi
            html.Should().Contain("Không thể tải danh sách đơn hàng cần hỗ trợ từ máy chủ");

            // Widget tồn kho: vẫn hoạt động bình thường và hiển thị danh sách sách sắp hết kho
            html.Should().Contain("Sách sắp hết kho");
            html.Should().NotContain("Không thể tải danh sách tồn kho từ máy chủ");
        }

        // ==========================================
        // 3. TEST CHUÔNG THÔNG BÁO: KHÔNG CÓ INCIDENTCOUNT
        // ==========================================
        [Fact]
        public async Task GetNeedsSupportCount_ReturnsCountAndDoesNotContainIncidentCount()
        {
            // Arrange
            var client = CreateClientForRole("Staff");

            // Act
            var response = await client.GetAsync("/Orders/GetNeedsSupportCount");
            var json = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            json.Should().Contain("\"count\":");
            json.Should().NotContain("incidentCount");
        }
    }
}
