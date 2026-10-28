using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Services;
using FluentAssertions;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class PosContractTests
    {
        private readonly MockPosApi _mockPosApi;

        public PosContractTests()
        {
            _mockPosApi = new MockPosApi();
        }

        [Fact]
        public async Task Pos_CreateOrder_ReturnsExactInternalMockQrPath()
        {
            // Act: Tạo đơn hàng tại quầy với phương thức QR
            var request = new CounterCheckoutRequestDto
            {
                PaymentMethod = "QR",
                Items = new List<CounterCheckoutItemDto>
                {
                    new CounterCheckoutItemDto { BookId = 9301, Quantity = 1 }
                }
            };

            var result = await _mockPosApi.CheckoutCounterAsync(request);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            // CMS-POS-01: Đường dẫn mock QR phải khớp CHÍNH XÁC "/images/mock-qr.svg"
            result.Data!.SepayQrCodeUrl.Should().Be("/images/mock-qr.svg");
        }

        [Fact]
        public async Task Pos_ConfirmPayment_SucceedsForValidOrder()
        {
            // Act: Tạo đơn hàng trước
            var request = new CounterCheckoutRequestDto
            {
                PaymentMethod = "Cash",
                Items = new List<CounterCheckoutItemDto>
                {
                    new CounterCheckoutItemDto { BookId = 9301, Quantity = 1 }
                }
            };
            var createResult = await _mockPosApi.CheckoutCounterAsync(request);
            createResult.Success.Should().BeTrue();

            // Xác nhận thanh toán
            var result = await _mockPosApi.ConfirmPaymentAsync(createResult.Data!.OrderId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.MemberDisplayName.Should().Be("Thành viên");
            // Không lộ SĐT
            typeof(PosReceiptDto).GetProperty("MemberPhone").Should().BeNull();
        }

        [Fact]
        public void PosPromotionDto_Deserializes_Type1_OrderDiscount_NestedPayload()
        {
            var json = """
            {
                "promotionId": 101,
                "name": "KM Khai Trương",
                "description": "Ưu đãi khách mới",
                "promotionType": 1,
                "startDate": "2026-10-01T00:00:00Z",
                "endDate": "2026-10-31T23:59:59Z",
                "isActive": true,
                "orderDiscount": {
                    "minOrderValue": 200000.0,
                    "discountAmount": 30000.0
                },
                "productDiscounts": []
            }
            """;

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var dto = System.Text.Json.JsonSerializer.Deserialize<PosPromotionDto>(json, options);

            dto.Should().NotBeNull();
            dto!.PromotionId.Should().Be(101);
            dto.PromotionType.Should().Be(BookKiosk.Domain.Enums.PromotionType.OrderDiscount);
            dto.EffectiveMinOrderValue.Should().Be(200000m);
            dto.EffectiveDiscountAmount.Should().Be(30000m);
            dto.SummaryText.Should().Be("Giảm 30,000 ₫ cho đơn từ 200,000 ₫");
            dto.SummaryText.Should().NotContain("Giảm 0 ₫");
        }

        [Fact]
        public void PosPromotionDto_Deserializes_Type2_ProductDiscount_NestedPayload()
        {
            var json = """
            {
                "promotionId": 102,
                "name": "KM Mua Sách Hay",
                "description": null,
                "promotionType": 2,
                "startDate": "2026-10-01T00:00:00Z",
                "endDate": "2026-10-31T23:59:59Z",
                "isActive": true,
                "orderDiscount": null,
                "productDiscounts": [
                    { "bookId": 9301, "discountAmount": 10000.0 },
                    { "bookId": 9302, "discountAmount": 15000.0 }
                ]
            }
            """;

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var dto = System.Text.Json.JsonSerializer.Deserialize<PosPromotionDto>(json, options);

            dto.Should().NotBeNull();
            dto!.PromotionId.Should().Be(102);
            dto.PromotionType.Should().Be(BookKiosk.Domain.Enums.PromotionType.ProductDiscount);
            dto.ProductDiscounts.Should().HaveCount(2);
            dto.SummaryText.Should().Be("Giảm theo sách: 2 đầu sách");
            dto.SummaryText.Should().NotContain("Giảm 0 ₫");
        }

        [Fact]
        public void PosPromotionDto_MissingData_ReturnsSafeNeutralLabel_NeverZeroVnd()
        {
            var json = """
            {
                "promotionId": 103,
                "name": "KM Đặc Biệt",
                "promotionType": 1,
                "orderDiscount": null,
                "productDiscounts": []
            }
            """;

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var dto = System.Text.Json.JsonSerializer.Deserialize<PosPromotionDto>(json, options);

            dto.Should().NotBeNull();
            dto!.EffectiveDiscountAmount.Should().Be(0);
            dto.SummaryText.Should().NotBe("Giảm 0 ₫");
            dto.SummaryText.Should().Be("Chi tiết theo thể lệ");
        }

        [Fact]
        public async Task MockPosApi_GetActivePromotions_ReturnsV71NestedPayload()
        {
            var result = await _mockPosApi.GetActivePromotionsAsync();

            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            var promos = new List<PosPromotionDto>(result.Data!);
            var p9201 = promos.Find(p => p.PromotionId == 9201);
            p9201.Should().NotBeNull();
            p9201!.PromotionType.Should().Be(BookKiosk.Domain.Enums.PromotionType.OrderDiscount);
            p9201.OrderDiscount.Should().NotBeNull();
            p9201.EffectiveDiscountAmount.Should().Be(30000m);
            p9201.SummaryText.Should().Be("Giảm 30,000 ₫ cho đơn từ 200,000 ₫");

            var p9204 = promos.Find(p => p.PromotionId == 9204);
            p9204.Should().NotBeNull();
            p9204!.PromotionType.Should().Be(BookKiosk.Domain.Enums.PromotionType.ProductDiscount);
            p9204.ProductDiscounts.Should().HaveCount(3);
            p9204.SummaryText.Should().Be("Giảm theo sách: 3 đầu sách");
            p9204.SummaryText.Should().NotContain("Giảm 0 ₫");
        }
    }
}
