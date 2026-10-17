using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Orders;

namespace BookKiosk.CMS.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    [Route("Orders")]
    [Route("Order")]
    public class OrdersController : Controller
    {
        private readonly IOrderApi _orderApi;
        private readonly IPosApi _posApi;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderApi orderApi,
            IPosApi posApi,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<OrdersController> logger)
        {
            _orderApi = orderApi;
            _posApi = posApi;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        // ==========================================
        // 1. DANH SÁCH ĐƠN HÀNG & ĐỐI SOÁT (INDEX)
        // ==========================================
        [HttpGet("")]
        public async Task<IActionResult> Index(
            string? tab = "all",
            string? orderCode = null,
            OrderStatus? status = null,
            SaleChannel? channel = null,
            PaymentMethod? paymentMethod = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool? needsSupport = null,
            int page = 1,
            int pageSize = 20)
        {
            ViewData["Title"] = "Quản lý Hóa đơn & Đối soát";

            var normalizedTab = (tab ?? "all").ToLower();
            bool isSupportTab = normalizedTab == "support" || (needsSupport.HasValue && needsSupport.Value);

            var filter = new OrderFilterDto
            {
                OrderCode = orderCode,
                Status = status,
                Channel = channel,
                PaymentMethod = paymentMethod,
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, 100)
            };

            // Chuyển đổi tab sang filter tương ứng
            if (isSupportTab)
            {
                filter.NeedsSupportOnly = true;
                normalizedTab = "support";
            }
            else if (normalizedTab == "kiosk")
            {
                filter.Channel = SaleChannel.Kiosk;
            }
            else if (normalizedTab == "counter")
            {
                filter.Channel = SaleChannel.Counter;
            }

            // Xử lý khoảng ngày
            if (fromDate.HasValue)
            {
                filter.FromDate = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
            }
            if (toDate.HasValue)
            {
                // Bao gồm đến cuối ngày 23:59:59.999
                filter.ToDate = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            }

            var ordersResult = await _orderApi.GetOrdersAsync(filter);
            var needsSupportCountResult = await _orderApi.GetNeedsSupportCountAsync();

            var viewModel = new OrderListViewModel
            {
                Filter = filter,
                ActiveTab = normalizedTab,
                NeedsSupportCount = needsSupportCountResult.Success ? needsSupportCountResult.Data : 0,
                IsDevelopment = _environment.IsDevelopment(),
                UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi")
            };

            if (ordersResult.IsEndpointUnavailable)
            {
                viewModel.IsEndpointUnavailable = true;
                viewModel.EndpointMessage = ordersResult.Message;
            }
            else if (ordersResult.Success && ordersResult.Data != null)
            {
                viewModel.Orders = ordersResult.Data;
            }

            return View(viewModel);
        }

        // ==========================================
        // 2. CHI TIẾT ĐƠN HÀNG (MODAL / PAGE)
        // ==========================================
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (id <= 0)
            {
                if (isAjax)
                    return NotFound(new { message = "Mã đơn hàng không hợp lệ." });

                Response.StatusCode = 404;
                ViewData["Title"] = "Không tìm thấy đơn hàng";
                return View(new OrderDetailViewModel());
            }

            var result = await _orderApi.GetOrderByIdAsync(id);
            var viewModel = new OrderDetailViewModel();

            if (result.IsEndpointUnavailable)
            {
                viewModel.IsEndpointUnavailable = true;
                viewModel.EndpointMessage = result.Message;
            }
            else if (result.Success && result.Data != null)
            {
                viewModel.Order = result.Data;
            }
            else
            {
                if (isAjax)
                    return NotFound(new { message = result.Message ?? "Không tìm thấy thông tin đơn hàng." });

                Response.StatusCode = 404;
                ViewData["Title"] = "Không tìm thấy đơn hàng";
                return View(viewModel);
            }

            // Nếu gọi qua AJAX (Modal popup) -> Trả PartialView
            if (isAjax)
            {
                return PartialView("_OrderDetailModal", viewModel);
            }

            var orderCode = viewModel.Order?.OrderCode;
            ViewData["Title"] = !string.IsNullOrWhiteSpace(orderCode)
                ? $"Chi tiết đơn hàng {orderCode}"
                : "Chi tiết đơn hàng";

            return View(viewModel);
        }

        // ==========================================
        // 3. IN LẠI HÓA ĐƠN K80 (CHỈ DÀNH CHO PAID)
        // ==========================================
        [HttpGet("Receipt/{id:int}")]
        public async Task<IActionResult> Receipt(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ." });

            var result = await _orderApi.GetOrderReceiptAsync(id);
            if (result.IsEndpointUnavailable)
            {
                return Json(new
                {
                    success = false,
                    isEndpointUnavailable = true,
                    statusCode = result.StatusCode,
                    message = result.Message
                });
            }

            return Json(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                message = result.Message,
                data = result.Data
            });
        }

        // ==========================================
        // 4. HỦY ĐƠN HÀNG QUẦY (COUNTER ONLY)
        // ==========================================
        [HttpPost("Cancel/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelCounterOrder(int id)
        {
            if (id <= 0)
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ." });

            var orderResult = await _orderApi.GetOrderByIdAsync(id);
            if (!orderResult.Success || orderResult.Data == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            // Chỉ cho phép hủy đơn Quầy Pending theo xác nhận Tech Lead
            if (orderResult.Data.SaleChannel != SaleChannel.Counter)
            {
                return Json(new { success = false, message = "Hệ thống hiện tại chưa hỗ trợ hủy đơn hàng từ Kiosk. CMS chỉ hỗ trợ xem và đối soát." });
            }

            if (orderResult.Data.OrderStatus != OrderStatus.Pending)
            {
                return Json(new { success = false, message = "Chỉ có thể hủy đơn hàng đang ở trạng thái Chờ thanh toán (Pending)." });
            }

            var cancelResult = await _posApi.CancelOrderAsync(id);
            return Json(new
            {
                success = cancelResult.Success,
                message = cancelResult.Success ? "Đã hủy đơn hàng và nhả kho giữ chỗ thành công." : cancelResult.Message
            });
        }

        // ==========================================
        // 5. API ĐẾM SỐ ĐƠN CẦN HỖ TRỢ (CHO CHUÔNG THÔNG BÁO)
        // ==========================================
        [HttpGet("GetNeedsSupportCount")]
        public async Task<IActionResult> GetNeedsSupportCount()
        {
            var result = await _orderApi.GetNeedsSupportCountAsync();
            return Json(new
            {
                count = result.Success ? result.Data : 0
            });
        }
    }
}
