using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Helpers;
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
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IReportApi reportApi,
            ICmsApiClient apiClient,
            ILogger<HomeController> logger)
        {
            _reportApi = reportApi;
            _apiClient = apiClient;
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
                SelectedYear = selectedYear
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

            // 3. Low Stock Items (AvailableStock <= 5, lấy tối đa 10 mục)
            try
            {
                var stockResult = await _apiClient.GetStockAsync(lowOnly: true);
                if (stockResult.Success && stockResult.Data != null)
                {
                    viewModel.LowStockItems = stockResult.Data
                        .Where(s => s.AvailableStock <= 5)
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

        [HttpGet("Home/GetRevenueChartData")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetRevenueChartData(int? year = null)
        {
            int selectedYear = year ?? DateTime.UtcNow.Year;
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
        public async Task<IActionResult> GetTopBooksJson()
        {
            try
            {
                var result = await _reportApi.GetTopSellingBooksAsync(10);
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
