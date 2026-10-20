using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Dashboard;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IReportApi _reportApi;
        private readonly ICmsApiClient _apiClient;
        private readonly IOrderApi _orderApi;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IReportApi reportApi,
            ICmsApiClient apiClient,
            IOrderApi orderApi,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            _reportApi = reportApi;
            _apiClient = apiClient;
            _orderApi = orderApi;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? year = null)
        {
            // Admin → Dashboard; Staff/Manager → không được xem dashboard doanh thu → POS
            if (!User.IsAdmin())
            {
                return RedirectToAction("Index", "Pos");
            }

            int selectedYear = year ?? DateTime.UtcNow.Year;

            var viewModel = new AdminDashboardViewModel
            {
                SelectedYear = selectedYear,
                UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi")
            };

            // Available years (5 years)
            int currentYear = DateTime.UtcNow.Year;
            for (int y = currentYear; y >= currentYear - 4; y--)
            {
                viewModel.AvailableYears.Add(y);
            }

            // 1. Revenue Report (Task 11.3)
            try
            {
                var revenueResult = await _reportApi.GetRevenueByMonthAsync(selectedYear);
                if (revenueResult.Success && revenueResult.Data != null)
                {
                    viewModel.RevenueReport = revenueResult.Data;
                }
                else
                {
                    viewModel.IsRevenueUnavailable = true;
                    _logger.LogWarning("Không tải được báo cáo doanh thu năm {Year}: {Message}", selectedYear, revenueResult.Message);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsRevenueUnavailable = true;
                _logger.LogError(ex, "Lỗi gọi API báo cáo doanh thu năm {Year}", selectedYear);
            }

            // 2. Top Selling Books (Top 10)
            try
            {
                var topBooksResult = await _reportApi.GetTopSellingBooksAsync(10);
                if (topBooksResult.Success && topBooksResult.Data != null)
                {
                    viewModel.TopBooks = topBooksResult.Data.Take(10).ToList();
                }
                else
                {
                    viewModel.IsTopBooksUnavailable = true;
                    _logger.LogWarning("Không tải được top sách bán chạy: {Message}", topBooksResult.Message);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsTopBooksUnavailable = true;
                _logger.LogError(ex, "Lỗi gọi API top sách bán chạy");
            }

            // 3. Low Stock Items (Lấy tối đa 10 mục do Backend đã lọc lowOnly)
            try
            {
                var stockResult = await _apiClient.GetStockAsync(lowOnly: true);
                if (stockResult.Success && stockResult.Data != null)
                {
                    viewModel.LowStockItems = stockResult.Data
                        .OrderBy(s => s.AvailableStock)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    viewModel.IsLowStockUnavailable = true;
                    _logger.LogWarning("Không tải được danh sách cảnh báo tồn kho: {Message}", stockResult.Message);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsLowStockUnavailable = true;
                _logger.LogError(ex, "Lỗi gọi API cảnh báo tồn kho");
            }

            return View(viewModel);
        }

        [HttpGet("Home/StaffWorkspace")]
        [Authorize(Roles = "Admin,Staff,Manager")]
        public async Task<IActionResult> StaffWorkspace()
        {
            var viewModel = new StaffWorkspaceViewModel
            {
                UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi")
            };

            // 1. Lấy danh sách cảnh báo tồn kho sắp hết (Lấy tối đa 10 mục do Backend đã lọc lowOnly)
            try
            {
                var stockResult = await _apiClient.GetStockAsync(lowOnly: true);
                if (stockResult.Success && stockResult.Data != null)
                {
                    viewModel.LowStockItems = stockResult.Data
                        .OrderBy(s => s.AvailableStock)
                        .Take(10)
                        .ToList();
                }
                else
                {
                    viewModel.IsLowStockUnavailable = true;
                    _logger.LogWarning("StaffWorkspace: Không tải được danh sách cảnh báo tồn kho: {Message}", stockResult.Message);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsLowStockUnavailable = true;
                _logger.LogError(ex, "StaffWorkspace: Lỗi gọi API cảnh báo tồn kho");
            }

            // 2. Lấy danh sách đơn hàng cần hỗ trợ (NeedsSupportOnly = true, tối đa 10 đơn)
            try
            {
                var orderFilter = new OrderFilterDto
                {
                    NeedsSupportOnly = true,
                    Page = 1,
                    PageSize = 10
                };
                var ordersResult = await _orderApi.GetOrdersAsync(orderFilter);
                if (ordersResult.Success && ordersResult.Data?.Items != null)
                {
                    viewModel.NeedsSupportOrders = ordersResult.Data.Items.Take(10).ToList();
                }
                else
                {
                    viewModel.IsOrdersUnavailable = true;
                    _logger.LogWarning("StaffWorkspace: Không tải được danh sách đơn hàng cần hỗ trợ: {Message}", ordersResult.Message);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsOrdersUnavailable = true;
                _logger.LogError(ex, "StaffWorkspace: Lỗi gọi API đơn hàng cần hỗ trợ");
            }

            return View(viewModel);
        }

        [HttpGet("Home/GetRevenueChartData")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRevenueChartData(int? year = null)
        {
            int selectedYear = year ?? DateTime.UtcNow.Year;
            if (selectedYear < 2000 || selectedYear > 2100)
            {
                return BadRequest(new { success = false, message = "Năm không hợp lệ. Vui lòng chọn năm trong khoảng từ 2000 đến 2100." });
            }

            try
            {
                var result = await _reportApi.GetRevenueByMonthAsync(selectedYear);
                if (!result.Success || result.Data == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Message ?? "Không thể lấy dữ liệu doanh thu."
                    });
                }

                var monthlyData = result.Data.MonthlyData;
                decimal totalRevenue = monthlyData.Sum(m => m.Revenue);
                int totalOrders = monthlyData.Sum(m => m.TotalOrders);

                return Json(new
                {
                    success = true,
                    year = result.Data.Year,
                    data = monthlyData,
                    totalRevenue,
                    totalOrders
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi AJAX GetRevenueChartData cho năm {Year}", selectedYear);
                return Json(new { success = false, message = "Lỗi máy chủ khi lấy dữ liệu biểu đồ." });
            }
        }

        [HttpGet("Home/GetTopBooksJson")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTopBooksJson(int? top = null)
        {
            int selectedTop = top ?? 10;
            if (top.HasValue && top.Value != 5 && top.Value != 10 && top.Value != 20)
            {
                return BadRequest(new { success = false, message = "Số lượng top sách chỉ chấp nhận 5, 10 hoặc 20." });
            }

            try
            {
                var result = await _reportApi.GetTopSellingBooksAsync(selectedTop);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi AJAX GetTopBooksJson");
                return Json(new { success = false, message = "Lỗi máy chủ khi lấy danh sách top sách." });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
