using System;
using System.Collections.Generic;
using System.Linq;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.ViewModels.Reports;

namespace BookKiosk.CMS.ViewModels.Dashboard
{
    public class AdminDashboardViewModel
    {
        public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
        public List<int> AvailableYears { get; set; } = new();

        public RevenueReportResponseDto? RevenueReport { get; set; }

        /// <summary>
        /// REVENUE-MONTHLY-01: Mảng 12 tháng đã được kiểm thực và chuẩn hóa duy nhất (không trùng lặp, zero-fill).
        /// </summary>
        public List<MonthlyReportRow> NormalizedMonthlyRows { get; set; } = new();

        public List<TopSellingBookDto> TopBooks { get; set; } = new();
        public List<StockItemDto> LowStockItems { get; set; } = new();

        private decimal? _totalAnnualRevenue;
        /// <summary>
        /// Tổng doanh thu năm tính từ bộ 12 tháng đã chuẩn hóa.
        /// </summary>
        public decimal TotalAnnualRevenue
        {
            get => _totalAnnualRevenue ?? (NormalizedMonthlyRows.Any()
                ? NormalizedMonthlyRows.Sum(m => m.Revenue)
                : (RevenueReport?.MonthlyData?.Sum(m => m.Revenue) ?? 0m));
            set => _totalAnnualRevenue = value;
        }

        private int? _totalAnnualOrders;
        /// <summary>
        /// Tổng đơn hàng năm tính từ bộ 12 tháng đã chuẩn hóa.
        /// </summary>
        public int TotalAnnualOrders
        {
            get => _totalAnnualOrders ?? (NormalizedMonthlyRows.Any()
                ? NormalizedMonthlyRows.Sum(m => m.TotalOrders)
                : (RevenueReport?.MonthlyData?.Sum(m => m.TotalOrders) ?? 0));
            set => _totalAnnualOrders = value;
        }

        /// <summary>
        /// Tổng số đầu sách sắp hết kho do Backend trả về (trước khi cắt danh sách hiển thị).
        /// </summary>
        public int TotalLowStockCount { get; set; }

        public int LowStockCount => TotalLowStockCount;

        public bool IsRevenueUnavailable { get; set; }
        public bool IsTopBooksUnavailable { get; set; }
        public bool IsLowStockUnavailable { get; set; }
        public bool UseMockApi { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
