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

        private List<MonthlyReportRow>? _monthlyRows;

        /// <summary>
        /// Chuẩn hóa mảng đủ 12 tháng (tháng 1 đến 12).
        /// REVENUE-MONTHLY-01: Dùng MonthlyRevenueNormalizer kiểm tra hợp đồng nghiêm ngặt.
        /// Tháng trùng hoặc tháng ngoài 1..12 sẽ bị TỪ CHỐI (IsRevenueUnavailable = true).
        /// </summary>
        public List<MonthlyReportRow> MonthlyRows
        {
            get
            {
                if (_monthlyRows != null) return _monthlyRows;

                var result = MonthlyRevenueNormalizer.Normalize(RevenueReport?.MonthlyData, SelectedYear);
                if (!result.IsValid)
                {
                    IsRevenueUnavailable = true;
                    if (string.IsNullOrWhiteSpace(ErrorMessage))
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                    return new List<MonthlyReportRow>();
                }

                _monthlyRows = result.Rows;
                return _monthlyRows;
            }
            set => _monthlyRows = value;
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
