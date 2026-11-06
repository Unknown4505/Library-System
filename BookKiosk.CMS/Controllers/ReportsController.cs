using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Reports;

namespace BookKiosk.CMS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Reports")]
    public class ReportsController : Controller
    {
        private readonly IReportApi _reportApi;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IReportApi reportApi,
            IConfiguration configuration,
            ILogger<ReportsController> logger)
        {
            _reportApi = reportApi;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(int? year = null)
        {
            var vnNow = DateTimeHelper.ToVietnamTime(DateTime.UtcNow);
            int currentYear = vnNow.Year;

            int selectedYear = currentYear;
            string? errorMessage = null;

            if (year.HasValue)
            {
                if (year.Value < 2000 || year.Value > 2100)
                {
                    errorMessage = $"Năm chọn ({year.Value}) không nằm trong khoảng hợp lệ (2000 - 2100). Hệ thống đã tự động quay về năm {currentYear}.";
                    selectedYear = currentYear;
                }
                else
                {
                    selectedYear = year.Value;
                }
            }

            // Danh sách 5 năm gần nhất theo giờ Việt Nam
            var availableYears = new HashSet<int>();
            for (int y = currentYear; y >= currentYear - 4; y--)
            {
                availableYears.Add(y);
            }
            if (selectedYear >= 2000 && selectedYear <= 2100)
            {
                availableYears.Add(selectedYear);
            }

            var viewModel = new ReportsIndexViewModel
            {
                SelectedYear = selectedYear,
                AvailableYears = availableYears.OrderByDescending(y => y).ToList(),
                UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi"),
                ApiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "",
                ErrorMessage = errorMessage
            };

            // 1. Gọi API Doanh thu 12 tháng
            try
            {
                var revenueResult = await _reportApi.GetRevenueByMonthAsync(selectedYear);
                if (revenueResult.Success && revenueResult.Data != null)
                {
                    var normResult = MonthlyRevenueNormalizer.Normalize(revenueResult.Data.MonthlyData, selectedYear);
                    if (normResult.IsValid)
                    {
                        viewModel.RevenueReport = revenueResult.Data;
                        viewModel.MonthlyRows = normResult.Rows;
                    }
                    else
                    {
                        viewModel.IsRevenueUnavailable = true;
                        viewModel.ErrorMessage = normResult.ErrorMessage;
                        _logger.LogWarning("Báo cáo doanh thu Reports năm {Year} vi phạm hợp đồng: {Reason}", selectedYear, normResult.ErrorMessage);
                    }
                }
                else
                {
                    viewModel.IsRevenueUnavailable = true;
                    viewModel.StatusCode = revenueResult.StatusCode;
                    _logger.LogWarning("Không tải được báo cáo doanh thu năm {Year}: {Message} (Status {Code})",
                        selectedYear, revenueResult.Message, revenueResult.StatusCode);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsRevenueUnavailable = true;
                _logger.LogError(ex, "Ngoại lệ khi gọi API báo cáo doanh thu năm {Year}", selectedYear);
            }

            // 2. Gọi API Top 10 Sách bán chạy
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
                    _logger.LogWarning("Không tải được top sách bán chạy: {Message} (Status {Code})",
                        topBooksResult.Message, topBooksResult.StatusCode);
                }
            }
            catch (Exception ex)
            {
                viewModel.IsTopBooksUnavailable = true;
                _logger.LogError(ex, "Ngoại lệ khi gọi API top sách bán chạy");
            }

            return View(viewModel);
        }
    }
}
