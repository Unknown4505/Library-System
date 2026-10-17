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
    }
}
