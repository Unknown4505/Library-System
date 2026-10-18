using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class OrdersRazorRenderTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public OrdersRazorRenderTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task Details_NotFound_RendersFriendlyNotFoundAndDoesNotRenderPendingOrCancelButton()
        {
            // Act: Gọi GET trực tiếp đến đơn hàng không tồn tại (999999)
            var response = await _client.GetAsync("/Orders/Details/999999");
            var html = await response.Content.ReadAsStringAsync();

            // Assert: Trả status 404
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Bắt buộc có thông báo 404 thân thiện
            html.Should().Contain("Không tìm thấy thông tin đơn hàng");

            // TUYỆT ĐỐI KHÔNG có trạng thái Pending, không có nút Hủy đơn, không có tổng tiền 0 ₫
            html.Should().NotContain("Chờ thanh toán (Pending)");
            html.Should().NotContain("badge bg-warning");
            html.Should().NotContain("Hủy đơn hàng");
            html.Should().NotContain("Tổng giá trị đơn hàng");
        }

        [Fact]
        public async Task Details_IdLessThanOrEqualToZero_RendersFriendlyNotFound()
        {
            // Act: Gọi GET trực tiếp với ID = 0
            var response = await _client.GetAsync("/Orders/Details/0");
            var html = await response.Content.ReadAsStringAsync();

            // Assert: Trả status 404
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            html.Should().Contain("Không tìm thấy thông tin đơn hàng");
            html.Should().NotContain("Chờ thanh toán (Pending)");
            html.Should().NotContain("Hủy đơn hàng");
        }

        [Fact]
        public async Task Details_Success_RendersFullOrderContent()
        {
            // Act: Gọi GET trực tiếp với đơn 1002 (Đơn Paid thành công)
            var response = await _client.GetAsync("/Orders/Details/1002");
            var html = await response.Content.ReadAsStringAsync();

            // Assert: Trả status 200
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("ORD-20261101-0102");
            html.Should().Contain("Đã thanh toán (Paid)");
            html.Should().Contain("In lại hóa đơn K80");
            html.Should().NotContain("Hủy đơn hàng");
        }

        [Fact]
        public async Task Details_CounterPending_RendersCancelButton()
        {
            // Act: Gọi GET trực tiếp với đơn 1005 (Đơn Quầy đang Pending)
            var response = await _client.GetAsync("/Orders/Details/1005");
            var html = await response.Content.ReadAsStringAsync();

            // Assert: Trả status 200
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("ORD-20261101-0109");
            html.Should().Contain("Chờ thanh toán (Pending)");
            html.Should().Contain("Hủy đơn hàng");
        }

        [Fact]
        public async Task Details_Ajax_NotFound_ReturnsJsonNotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/Orders/Details/999999");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Không tìm thấy");
        }

        [Fact]
        public async Task Details_Ajax_IdZero_ReturnsJsonNotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/Orders/Details/0");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Mã đơn hàng không hợp lệ");
        }

        [Fact]
        public async Task Details_EndpointUnavailable_RendersFriendlyWarningCard()
        {
            var mockOrderApi = new Mock<IOrderApi>();
            mockOrderApi.Setup(x => x.GetOrderByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(ApiResult<OrderDetailDto>.EndpointUnavailable("/api/orders/777", "GET", 404));

            var customClient = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddScoped(_ => mockOrderApi.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await customClient.GetAsync("/Orders/Details/777");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Endpoint máy chủ chưa sẵn sàng");
            html.Should().NotContain("Chờ thanh toán (Pending)");
            html.Should().NotContain("Hủy đơn hàng");
            html.Should().NotContain("Tổng giá trị đơn hàng");
        }

        [Fact]
        public async Task Details_Ajax_Found_RendersPartialModalWithOrderContent()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/Orders/Details/1002");
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var response = await _client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("ORD-20261101-0102");
            html.Should().Contain("Đã thanh toán (Paid)");
            html.Should().Contain("In lại hóa đơn K80");
            html.Should().NotContain("Không tìm thấy thông tin đơn hàng");
        }
    }
}
