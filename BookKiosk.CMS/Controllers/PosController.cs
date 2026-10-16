using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Pos;

namespace BookKiosk.CMS.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class PosController : Controller
    {
        private readonly IPosApi _posApi;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PosController> _logger;

        public PosController(
            IPosApi posApi,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<PosController> logger)
        {
            _posApi = posApi;
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        // ==========================================
        // 1. MÀN HÌNH CHÍNH POS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Bán tại quầy (POS)";

            var cashierName = User.FindFirstValue(ClaimTypes.Name) 
                              ?? User.FindFirstValue(ClaimTypes.NameIdentifier) 
                              ?? "Thu ngân";

            var viewModel = new PosIndexViewModel
            {
                IsDevelopment = _environment.IsDevelopment(),
                UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi"),
                OrderTimeoutMinutes = _configuration.GetValue<int>("Pos:OrderTimeoutMinutes", 4),
                ShowMemberNameOnReceipt = _configuration.GetValue<bool>("Pos:ShowMemberNameOnReceipt", false),
                CashierName = cashierName
            };

            // Khởi tạo trước danh mục sách cho bộ lọc
            var categoriesResult = await _posApi.GetCategoriesAsync();
            if (categoriesResult.Success && categoriesResult.Data != null)
            {
                viewModel.Categories = categoriesResult.Data;
            }

            // Khởi tạo danh sách khuyến mãi đang hoạt động
            var promoResult = await _posApi.GetActivePromotionsAsync();
            if (promoResult.Success && promoResult.Data != null)
            {
                viewModel.ActivePromotions = promoResult.Data;
            }

            return View(viewModel);
        }

        // ==========================================
        // 2. SCAN BARCODE (Súng quét hoặc nhập tay)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> ScanBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Json(new { success = false, message = "Vui lòng nhập hoặc quét mã vạch hợp lệ." });
            }

            var result = await _posApi.GetBookByBarcodeAsync(barcode);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 3. TÌM KIẾM SÁCH THỦ CÔNG (Khi barcode hỏng)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> SearchBooks(string? keyword, int? categoryId, int page = 1, int pageSize = 12)
        {
            var result = await _posApi.SearchBooksAsync(keyword, categoryId, page, pageSize);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 4. LẤY DANH SÁCH KHUYẾN MÃI HOÀNG LỰC
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetActivePromotions()
        {
            var result = await _posApi.GetActivePromotionsAsync();
            return FormatApiResponse(result);
        }

        // ==========================================
        // 5. TRA CỨU THÀNH VIÊN THEO SỐ ĐIỆN THOẠI
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> LookupMember(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new { success = false, message = "Vui lòng cung cấp số điện thoại thành viên." });
            }

            var result = await _posApi.GetMemberByPhoneAsync(phone);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 6. ĐĂNG KÝ NHANH THÀNH VIÊN TẠI QUẦY
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickRegisterMember([FromBody] PosCreateMemberDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Dữ liệu đăng ký thành viên không hợp lệ." });
            }

            var result = await _posApi.CreateMemberAsync(dto);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 7. CHECKOUT ĐƠN HÀNG QUẦY (Đơn nháp Pending)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout([FromBody] CounterCheckoutRequestDto dto)
        {
            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                return Json(new { success = false, message = "Giỏ hàng rỗng, không thể tạo đơn hàng." });
            }

            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                {
                    return Json(new { success = false, message = $"Số lượng sách #{item.BookId} phải lớn hơn 0." });
                }
            }

            if (dto.PointsToUse < 0 || dto.PointsToUse > 100)
            {
                return Json(new { success = false, message = "Số điểm sử dụng phải từ 0 đến 100 điểm." });
            }

            var result = await _posApi.CheckoutCounterAsync(dto);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 8. XÁC NHẬN THANH TOÁN (Thu tiền mặt hoặc QR)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ." });
            }

            var result = await _posApi.ConfirmPaymentAsync(orderId);
            
            // Xử lý bảo mật thông tin trên Hóa đơn K80 & nạp thông tin cửa hàng
            if (result.Success && result.Data != null)
            {
                var cashierName = User.FindFirstValue(ClaimTypes.Name) 
                                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier) 
                                  ?? "Thu ngân";

                result.Data.CashierName = cashierName;

                // Nạp thông tin cửa hàng từ appsettings (Pos:StoreInfo)
                result.Data.StoreName = _configuration.GetValue<string>("Pos:StoreInfo:Name") ?? result.Data.StoreName;
                result.Data.StoreAddress = _configuration.GetValue<string>("Pos:StoreInfo:Address") ?? result.Data.StoreAddress;
                result.Data.StoreHotline = _configuration.GetValue<string>("Pos:StoreInfo:Hotline") ?? result.Data.StoreHotline;
                result.Data.StoreWebsite = _configuration.GetValue<string>("Pos:StoreInfo:Website") ?? result.Data.StoreWebsite;

                // Quy tắc bảo mật: Mặc định không in tên thành viên trừ khi cấu hình cho phép
                var showMemberName = _configuration.GetValue<bool>("Pos:ShowMemberNameOnReceipt", false);
                if (!showMemberName)
                {
                    result.Data.MemberDisplayName = "Thành viên";
                }
            }

            return FormatApiResponse(result);
        }

        // ==========================================
        // 9. HỦY ĐƠN HÀNG QUẦY (Nhả kho giữ chỗ)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ." });
            }

            var result = await _posApi.CancelOrderAsync(orderId);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 10. POLLING TRẠNG THÁI ĐƠN HÀNG (Nhánh QR)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> PollStatus(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ." });
            }

            var result = await _posApi.GetOrderStatusAsync(orderId);
            return FormatApiResponse(result);
        }

        // ==========================================
        // 11. CONTRACT VALIDATION TEST (Bước C & D - Chỉ chạy ở Dev/Debug)
        // ==========================================
#if DEBUG
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ValidateContracts()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "Tests", "Fixtures"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Tests", "Fixtures"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "BookKiosk.CMS", "Tests", "Fixtures")
            };

            string? fixturesDir = null;
            foreach (var candidate in candidates)
            {
                if (System.IO.Directory.Exists(candidate))
                {
                    fixturesDir = candidate;
                    break;
                }
            }

            if (fixturesDir == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thư mục Tests/Fixtures" });
            }

            var passed = BookKiosk.CMS.Tests.PosContractValidator.ValidateAll(fixturesDir, out var errors);
            return Json(new
            {
                success = passed,
                fixtureCount = 4,
                errorCount = errors.Count,
                errors,
                fixturesPath = fixturesDir
            });
        }
#endif

        // ==========================================
        // HELPER CHUNG: Chuẩn hóa Response JSON cho UI
        // ==========================================
        private JsonResult FormatApiResponse<T>(Models.Common.ApiResult<T> result)
        {
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
                isEndpointUnavailable = false,
                statusCode = result.StatusCode,
                message = result.Message,
                data = result.Data
            });
        }
    }
}
