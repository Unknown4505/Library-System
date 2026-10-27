using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Services;
using BookKiosk.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class OrderReprintV71ContractTests
    {
        private static string GetCmsRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "BookKiosk.CMS"));
            if (Directory.Exists(candidate)) return candidate;

            candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "BookKiosk.CMS"));
            if (Directory.Exists(candidate)) return candidate;

            throw new DirectoryNotFoundException("Không tìm thấy thư mục BookKiosk.CMS");
        }

        [Theory]
        [InlineData("1", PromotionType.OrderDiscount)]
        [InlineData("2", PromotionType.ProductDiscount)]
        [InlineData("\"OrderDiscount\"", PromotionType.OrderDiscount)]
        [InlineData("\"ProductDiscount\"", PromotionType.ProductDiscount)]
        [InlineData("\"1\"", PromotionType.OrderDiscount)]
        [InlineData("\"2\"", PromotionType.ProductDiscount)]
        [InlineData("null", null)]
        [InlineData("\"\"", null)]
        [InlineData("99", null)]
        public void FlexibleNullableEnumConverter_ParsesVariousTokenFormats(string jsonSnippet, PromotionType? expected)
        {
            var json = $"{{\"type\": {jsonSnippet}}}";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var result = JsonSerializer.Deserialize<TestWrapperDto>(json, options);

            result.Should().NotBeNull();
            result!.Type.Should().Be(expected);
        }

        private class TestWrapperDto
        {
            [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleNullableEnumConverter<PromotionType>))]
            public PromotionType? Type { get; set; }
        }

        [Fact]
        public void OrderDetailDto_DeserializesV71BackendPayload_Correctly()
        {
            var json = @"{
                ""orderId"": 1007,
                ""orderCode"": ""ORD-20261101-0111"",
                ""orderStatus"": 2,
                ""subTotal"": 230000,
                ""promotionId"": 2,
                ""promotionName"": ""Giảm 10k/cuốn"",
                ""promotionType"": 2,
                ""discountAmount"": 20000,
                ""pointsUsed"": 0,
                ""pointsUsedAmount"": 0,
                ""totalAmount"": 210000,
                ""paymentMethod"": ""QR"",
                ""items"": [
                    {
                        ""bookId"": 9304,
                        ""bookTitle"": ""Đắc Nhân Tâm"",
                        ""quantity"": 2,
                        ""unitPriceAtTime"": 85000,
                        ""discountPerUnit"": 10000,
                        ""lineTotal"": 170000,
                        ""lineDiscountAmount"": 20000,
                        ""netLineTotal"": 150000
                    },
                    {
                        ""bookId"": 9303,
                        ""bookTitle"": ""Số Đỏ"",
                        ""quantity"": 1,
                        ""unitPriceAtTime"": 60000,
                        ""discountPerUnit"": 0,
                        ""lineTotal"": 60000,
                        ""lineDiscountAmount"": 0,
                        ""netLineTotal"": 60000
                    }
                ]
            }";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<OrderDetailDto>(json, options);

            dto.Should().NotBeNull();
            dto!.OrderId.Should().Be(1007);
            dto.PromotionId.Should().Be(2);
            dto.PromotionName.Should().Be("Giảm 10k/cuốn");
            dto.PromotionType.Should().Be(PromotionType.ProductDiscount);
            dto.DiscountAmount.Should().Be(20000m);
            dto.Items.Should().HaveCount(2);

            var item1 = dto.Items[0];
            item1.BookId.Should().Be(9304);
            item1.DiscountPerUnit.Should().Be(10000m);
            item1.LineDiscountAmount.Should().Be(20000m);
            item1.NetLineTotal.Should().Be(150000m);

            var item2 = dto.Items[1];
            item2.BookId.Should().Be(9303);
            item2.DiscountPerUnit.Should().Be(0m);
            item2.LineDiscountAmount.Should().Be(0m);
            item2.NetLineTotal.Should().Be(60000m);
        }

        [Fact]
        public async Task MockOrderApi_GetOrderReceiptAsync_MapsV71FieldsCorrectly()
        {
            var config = new ConfigurationBuilder().Build();
            var api = new MockOrderApi(config);

            var result = await api.GetOrderReceiptAsync(1007);

            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            var receipt = result.Data!;
            receipt.OrderCode.Should().Be("ORD-20261101-0111");
            receipt.PromotionId.Should().Be(2);
            receipt.PromotionName.Should().Be("Giảm 10k/cuốn Đắc Nhân Tâm");
            receipt.PromotionType.Should().Be(PromotionType.ProductDiscount);
            receipt.DiscountAmount.Should().Be(20000m);
            receipt.TotalAmount.Should().Be(210000m);
            receipt.Items.Should().HaveCount(2);

            var row1 = receipt.Items[0];
            row1.DiscountPerUnit.Should().Be(10000m);
            row1.LineDiscountAmount.Should().Be(20000m);
            row1.NetLineTotal.Should().Be(150000m);

            var row2 = receipt.Items[1];
            row2.DiscountPerUnit.Should().Be(0m);
            row2.LineDiscountAmount.Should().Be(0m);
            row2.NetLineTotal.Should().Be(60000m);
        }

        [Fact]
        public void ReceiptModal_ContainsV71ElementsAndScreenOnlyNotice()
        {
            var modalPath = Path.Combine(GetCmsRoot(), "Views", "Pos", "_ReceiptModal.cshtml");
            File.Exists(modalPath).Should().BeTrue();

            var content = File.ReadAllText(modalPath);

            content.Should().Contain("receiptDiscountLabel");
            content.Should().Contain("receiptUnassignedNotice");
            content.Should().Contain("d-print-none");
            content.Should().Contain("Chi tiết giảm giá từng sách chưa có từ máy chủ");
        }

        [Fact]
        public void OrdersJs_ImplementsV71DiscountBreakdownAndQ1Rule()
        {
            var jsPath = Path.Combine(GetCmsRoot(), "wwwroot", "js", "orders.js");
            File.Exists(jsPath).Should().BeTrue();

            var content = File.ReadAllText(jsPath);

            content.Should().Contain("lineDiscountAmount");
            content.Should().Contain("receiptUnassignedNotice");
            content.Should().Contain("receiptDiscountLabel");
            content.Should().Contain("netLineTotal");
            // Đảm bảo không dùng innerHTML để chèn dữ liệu dòng
            content.Should().NotContain("tr.innerHTML =");
        }
    }
}
