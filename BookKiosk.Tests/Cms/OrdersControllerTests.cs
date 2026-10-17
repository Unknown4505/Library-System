using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Orders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class OrdersControllerTests
    {
        private readonly Mock<IOrderApi> _orderApiMock;
        private readonly Mock<IPosApi> _posApiMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<OrdersController>> _loggerMock;

        public OrdersControllerTests()
        {
            _orderApiMock = new Mock<IOrderApi>();
            _posApiMock = new Mock<IPosApi>();
            _configMock = new Mock<IConfiguration>();
            _envMock = new Mock<IWebHostEnvironment>();
            _loggerMock = new Mock<ILogger<OrdersController>>();
        }

        private OrdersController CreateController(HttpContext? httpContext = null)
        {
            var controller = new OrdersController(
                _orderApiMock.Object,
                _posApiMock.Object,
                _configMock.Object,
                _envMock.Object,
                _loggerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext ?? new DefaultHttpContext()
                }
            };
            return controller;
        }

        [Fact]
        public async Task Details_DirectGetNonAjax_ReturnsFullViewWithModel()
        {
            // Arrange
            _orderApiMock.Setup(o => o.GetOrderByIdAsync(101))
                .ReturnsAsync(ApiResult<OrderDetailDto>.Ok(new OrderDetailDto
                {
                    OrderId = 101,
                    OrderCode = "ORD-20261017-001",
                    OrderStatus = OrderStatus.Paid,
                    SaleChannel = SaleChannel.Counter,
                    TotalAmount = 150000,
                    CreatedAt = DateTime.UtcNow
                }));

            var controller = CreateController();

            // Act
            var result = await controller.Details(101);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var vm = viewResult.Model.Should().BeOfType<OrderDetailViewModel>().Subject;
            vm.Order.OrderCode.Should().Be("ORD-20261017-001");
            controller.ViewData["Title"].Should().Be("Chi tiết đơn hàng ORD-20261017-001");
        }

        [Fact]
        public async Task Details_NonExistentOrder_ReturnsViewResultWithStatusCode404()
        {
            // Arrange: Backend trả về lỗi 404
            _orderApiMock.Setup(o => o.GetOrderByIdAsync(999))
                .ReturnsAsync(ApiResult<OrderDetailDto>.Fail("Không tìm thấy đơn hàng #999.", 404));

            var context = new DefaultHttpContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.Details(999);

            // Assert: Không return NotFound() văng lỗi IIS chuẩn, mà trả ViewResult với Response.StatusCode = 404
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            context.Response.StatusCode.Should().Be(404);
            controller.ViewData["Title"].Should().Be("Không tìm thấy đơn hàng");
        }

        [Fact]
        public async Task Details_EndpointUnavailable_ReturnsFriendlyViewWithoutCrashing()
        {
            // Arrange: Giả lập backend endpoint /api/orders/{id} bị 404/405 không bọc wrapper
            _orderApiMock.Setup(o => o.GetOrderByIdAsync(888))
                .ReturnsAsync(ApiResult<OrderDetailDto>.EndpointUnavailable("/api/orders/888", "GET", 404));

            var controller = CreateController();

            // Act: Không văng NullReferenceException
            var result = await controller.Details(888);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var vm = viewResult.Model.Should().BeOfType<OrderDetailViewModel>().Subject;
            vm.IsEndpointUnavailable.Should().BeTrue();
            // Không được dùng "N/A" làm title khi orderCode rỗng
            var title = controller.ViewData["Title"]?.ToString();
            title.Should().Be("Chi tiết đơn hàng");
        }

        [Fact]
        public async Task Details_AjaxRequest_ReturnsPartialView()
        {
            // Arrange: Header X-Requested-With = XMLHttpRequest
            _orderApiMock.Setup(o => o.GetOrderByIdAsync(102))
                .ReturnsAsync(ApiResult<OrderDetailDto>.Ok(new OrderDetailDto
                {
                    OrderId = 102,
                    OrderCode = "ORD-20261017-002",
                    OrderStatus = OrderStatus.Pending,
                    SaleChannel = SaleChannel.Counter
                }));

            var context = new DefaultHttpContext();
            context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
            var controller = CreateController(context);

            // Act
            var result = await controller.Details(102);

            // Assert
            var partial = result.Should().BeOfType<PartialViewResult>().Subject;
            partial.ViewName.Should().Be("_OrderDetailModal");
        }

        // ==============================================================
        // ORDERS-RECEIPT TESTS (Requirement 5)
        // ==============================================================

        [Theory]
        [InlineData(OrderStatus.Pending)]
        [InlineData(OrderStatus.Cancelled)]
        public async Task Receipt_OrderNotPaid_ReturnsFailWithStatusCode409(OrderStatus nonPaidStatus)
        {
            // Arrange: CmsOrderApi / MockOrderApi trả về 409 khi đơn hàng chưa thanh toán
            _orderApiMock.Setup(o => o.GetOrderReceiptAsync(201))
                .ReturnsAsync(ApiResult<PosReceiptDto>.Fail(
                    $"Chỉ có thể in lại hóa đơn cho đơn hàng đã thanh toán thành công (Paid). Trạng thái hiện tại: {nonPaidStatus}",
                    409));

            var controller = CreateController();

            // Act
            var result = await controller.Receipt(201);

            // Assert: Json result có success = false và statusCode = 409
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            jsonResult.Value.Should().NotBeNull();

            // Dùng reflection để đọc anonymous object
            var val = jsonResult.Value!;
            var successProp = val.GetType().GetProperty("success")?.GetValue(val);
            var statusProp = val.GetType().GetProperty("statusCode")?.GetValue(val);
            var messageProp = val.GetType().GetProperty("message")?.GetValue(val);

            successProp.Should().Be(false);
            statusProp.Should().Be(409);
            messageProp?.ToString().Should().Contain("Chỉ có thể in lại hóa đơn cho đơn hàng đã thanh toán thành công (Paid)");
        }

        [Fact]
        public async Task Receipt_OrderPaid_ReturnsSuccessWithReceiptData()
        {
            // Arrange: Đơn hàng Paid thành công
            var expectedReceipt = new PosReceiptDto
            {
                OrderCode = "ORD-20261017-PAID",
                CashierName = "Thu ngân Quầy 1",
                MemberDisplayName = "Thành viên",
                TotalAmount = 250000,
                PaymentMethod = "QR"
            };

            _orderApiMock.Setup(o => o.GetOrderReceiptAsync(202))
                .ReturnsAsync(ApiResult<PosReceiptDto>.Ok(expectedReceipt));

            var controller = CreateController();

            // Act
            var result = await controller.Receipt(202);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var val = jsonResult.Value!;
            var successProp = val.GetType().GetProperty("success")?.GetValue(val);
            var dataProp = val.GetType().GetProperty("data")?.GetValue(val) as PosReceiptDto;

            successProp.Should().Be(true);
            dataProp.Should().NotBeNull();
            dataProp!.OrderCode.Should().Be("ORD-20261017-PAID");
        }

        [Fact]
        public async Task Receipt_PaidOrder_ExcludesMemberNameAndPhone()
        {
            // Arrange: Đơn hàng có thông tin khách hàng nhạy cảm (SĐT và Tên)
            // Model PosReceiptDto hoàn toàn KHÔNG có thuộc tính MemberPhone
            // và MemberDisplayName mặc định là "Thành viên" khi chưa bật cấu hình ShowMemberNameOnReceipt
            var receipt = new PosReceiptDto
            {
                OrderCode = "ORD-20261017-PRIVACY",
                CashierName = "Thu ngân Quầy 1",
                MemberDisplayName = "Thành viên", // Đã loại bỏ tên thực
                TotalAmount = 180000,
                PaymentMethod = "Cash"
            };

            _orderApiMock.Setup(o => o.GetOrderReceiptAsync(203))
                .ReturnsAsync(ApiResult<PosReceiptDto>.Ok(receipt));

            var controller = CreateController();

            // Act
            var result = await controller.Receipt(203);

            // Assert
            var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
            var val = jsonResult.Value!;
            var dataProp = val.GetType().GetProperty("data")?.GetValue(val) as PosReceiptDto;

            dataProp.Should().NotBeNull();
            // 1. Không hiển thị tên thực cá nhân nếu cấu hình mặc định tắt
            dataProp!.MemberDisplayName.Should().Be("Thành viên");
            // 2. PosReceiptDto không có thuộc tính MemberPhone nào để ngăn rò rỉ SĐT
            typeof(PosReceiptDto).GetProperty("MemberPhone").Should().BeNull();
        }
    }
}
