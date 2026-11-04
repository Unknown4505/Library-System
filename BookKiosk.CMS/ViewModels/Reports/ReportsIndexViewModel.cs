using System;
using System.Collections.Generic;
using System.Linq;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.Models.Reports;

namespace BookKiosk.CMS.ViewModels.Reports
{
    public class MonthlyReportRow
    {
        public int Month { get; set; }
        public decimal Revenue { get; set; }
        public int TotalOrders { get; set; }

        /// <summary>
        /// Trung bình / đơn của từng tháng: totalOrders > 0 ? revenue / totalOrders : 0 (làm tròn số nguyên VNĐ)
        /// </summary>
        public decimal AverageOrderValue => TotalOrders > 0 ? Math.Round(Revenue / TotalOrders) : 0m;
    }

    public class ReportsIndexViewModel
    {
        public int SelectedYear { get; set; } = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).Year;
        public List<int> AvailableYears { get; set; } = new();

        public RevenueReportResponseDto? RevenueReport { get; set; }
        public List<TopSellingBookDto> TopBooks { get; set; } = new();

        public string? ApiBaseUrl { get; set; }
        public bool UseMockApi { get; set; }

        public bool IsRevenueUnavailable { get; set; }
        public bool IsTopBooksUnavailable { get; set; }
        public int? StatusCode { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Chuẩn hóa mảng đủ 12 tháng (tháng 1 đến 12).
        /// Tháng nào Backend không có dữ liệu thì gán mặc định bằng 0.
        /// REPORT-RISK-01: An toàn tuyệt đối trước dữ liệu trùng tháng từ Backend/Mock (dùng GroupBy và gộp cộng dồn).
        /// </summary>
        public List<MonthlyReportRow> MonthlyRows
        {
            get
            {
                var rows = new List<MonthlyReportRow>();
                var dict = RevenueReport?.MonthlyData?
                    .Where(m => m.Month >= 1 && m.Month <= 12)
                    .GroupBy(m => m.Month)
                    .ToDictionary(
                        g => g.Key,
                        g => new MonthlyRevenueItemDto
                        {
                            Month = g.Key,
                            Revenue = g.Sum(x => x.Revenue),
                            TotalOrders = g.Sum(x => x.TotalOrders)
                        }) ?? new Dictionary<int, MonthlyRevenueItemDto>();

                for (int m = 1; m <= 12; m++)
                {
                    if (dict.TryGetValue(m, out var item))
                    {
                        rows.Add(new MonthlyReportRow
                        {
                            Month = m,
                            Revenue = item.Revenue,
                            TotalOrders = item.TotalOrders
                        });
                    }
                    else
                    {
                        rows.Add(new MonthlyReportRow
                        {
                            Month = m,
                            Revenue = 0m,
                            TotalOrders = 0
                        });
                    }
                }

                return rows;
            }
        }

        /// <summary>
        /// Tổng doanh thu cả năm = cộng dồn từ 12 tháng
        /// </summary>
        public decimal TotalAnnualRevenue => MonthlyRows.Sum(r => r.Revenue);

        /// <summary>
        /// Tổng số đơn hàng cả năm = cộng dồn từ 12 tháng
        /// </summary>
        public int TotalAnnualOrders => MonthlyRows.Sum(r => r.TotalOrders);

        /// <summary>
        /// Giá trị trung bình cả năm tính từ TỔNG: TotalAnnualOrders > 0 ? TotalAnnualRevenue / TotalAnnualOrders : 0
        /// </summary>
        public decimal AnnualAverageOrderValue => TotalAnnualOrders > 0
            ? Math.Round(TotalAnnualRevenue / TotalAnnualOrders)
            : 0m;

        /// <summary>
        /// Ghép URL an toàn cho ảnh bìa sách với ApiBaseUrl khi ảnh là đường dẫn tương đối
        /// </summary>
        public string GetSafeImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return "https://placehold.co/44x60/f8f9fa/6c757d?text=No+Img";
            }

            var trimmed = imageUrl.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("/"))
            {
                return string.IsNullOrWhiteSpace(ApiBaseUrl)
                    ? trimmed
                    : $"{ApiBaseUrl.TrimEnd('/')}{trimmed}";
            }

            return trimmed;
        }
    }
}
