using System;
using System.Collections.Generic;
using System.Linq;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Reports;

namespace BookKiosk.CMS.ViewModels.Dashboard
{
    public class AdminDashboardViewModel
    {
        public int SelectedYear { get; set; } = DateTime.UtcNow.Year;
        public List<int> AvailableYears { get; set; } = new();

        public RevenueReportResponseDto? RevenueReport { get; set; }
        public List<TopSellingBookDto> TopBooks { get; set; } = new();
        public List<StockItemDto> LowStockItems { get; set; } = new();

        /// <summary>
        /// Rule 3: Tổng doanh thu năm cộng bằng decimal C# từ 12 giá trị backend trả về.
        /// Ngoại lệ có tên: "Client-side Display Sum from Backend 12-Month Array"
        /// </summary>
        public decimal TotalAnnualRevenue => RevenueReport?.MonthlyData?.Sum(m => m.Revenue) ?? 0m;

        /// <summary>
        /// Rule 3: Tổng đơn năm cộng bằng int C# từ 12 giá trị backend trả về.
        /// </summary>
        public int TotalAnnualOrders => RevenueReport?.MonthlyData?.Sum(m => m.TotalOrders) ?? 0;

        public int LowStockCount => LowStockItems.Count;

        public bool IsRevenueUnavailable { get; set; }
        public bool IsTopBooksUnavailable { get; set; }
        public bool IsLowStockUnavailable { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
