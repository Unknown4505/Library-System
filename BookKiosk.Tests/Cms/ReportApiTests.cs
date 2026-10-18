using System.Net;
using System.Text.Json;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class ReportApiTests
    {
        private readonly Mock<ILogger<CmsReportApi>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfig;

        public ReportApiTests()
        {
            _mockLogger = new Mock<ILogger<CmsReportApi>>();
            _mockConfig = new Mock<IConfiguration>();
        }

        [Fact]
        public async Task GetRevenueByMonthAsync_Returns_12Months_MappedCorrectly()
        {
            // Arrange: Giả lập Backend thật trả về ApiResponseDto<object> có Year và Data 12 tháng
            var backendJson = """
            {
                "success": true,
                "code": "SUCCESS",
                "message": "Thành công",
                "data": {
                    "year": 2026,
                    "data": [
                        { "month": 1, "revenue": 15000000.0, "totalOrders": 120 },
                        { "month": 2, "revenue": 20000000.0, "totalOrders": 150 },
                        { "month": 3, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 4, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 5, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 6, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 7, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 8, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 9, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 10, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 11, "revenue": 0.0, "totalOrders": 0 },
                        { "month": 12, "revenue": 0.0, "totalOrders": 0 }
                    ]
                }
            }
            """;

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString().Contains("/api/reports/revenue?year=2026")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(backendJson, System.Text.Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://localhost:7111")
            };

            var reportApi = new CmsReportApi(httpClient, _mockConfig.Object, _mockLogger.Object);

            // Act
            var result = await reportApi.GetRevenueByMonthAsync(2026);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Year.Should().Be(2026);
            result.Data.MonthlyData.Should().HaveCount(12);
            result.Data.MonthlyData[0].Month.Should().Be(1);
            result.Data.MonthlyData[0].Revenue.Should().Be(15000000.0m);
            result.Data.MonthlyData[0].TotalOrders.Should().Be(120);
            result.Data.MonthlyData[2].Revenue.Should().Be(0m);
        }

        [Fact]
        public async Task GetTopSellingBooksAsync_Returns_TopBooks_MappedCorrectly()
        {
            // Arrange: Giả lập Backend thật trả về ApiResponseDto<object> bọc mảng top books
            var backendJson = """
            {
                "success": true,
                "code": "SUCCESS",
                "message": "Thành công",
                "data": [
                    { "bookId": 1, "title": "Đất Rừng Phương Nam", "imageUrl": "/images/books/dat-rung.jpg", "totalSold": 150, "totalRevenue": 12000000.0 },
                    { "bookId": 2, "title": "Tuổi Trẻ Đáng Giá", "imageUrl": "/images/books/tuoi-tre.jpg", "totalSold": 95, "totalRevenue": 7600000.0 }
                ]
            }
            """;

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString().Contains("/api/reports/top-books?top=10")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(backendJson, System.Text.Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://localhost:7111")
            };

            var reportApi = new CmsReportApi(httpClient, _mockConfig.Object, _mockLogger.Object);

            // Act
            var result = await reportApi.GetTopSellingBooksAsync(10);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            var list = result.Data!.ToList();
            list.Should().HaveCount(2);
            list[0].BookId.Should().Be(1);
            list[0].Title.Should().Be("Đất Rừng Phương Nam");
            list[0].TotalSold.Should().Be(150);
            list[0].TotalRevenue.Should().Be(12000000.0m);
        }

        [Fact]
        public async Task GetRevenueByMonthAsync_Returns_EndpointUnavailable_When404WithoutWrapper()
        {
            // Arrange: Giả lập 404 Kỹ thuật (Endpoint chưa triển khai trên Backend, không có JSON wrapper)
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent(string.Empty)
                });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://localhost:7111")
            };

            var reportApi = new CmsReportApi(httpClient, _mockConfig.Object, _mockLogger.Object);

            // Act
            var result = await reportApi.GetRevenueByMonthAsync(2026);

            // Assert
            result.Success.Should().BeFalse();
            result.IsEndpointUnavailable.Should().BeTrue();
            result.StatusCode.Should().Be(404);
        }
    }
}
