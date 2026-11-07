using System;
using System.Collections.Generic;
using System.Linq;
using BookKiosk.CMS.Models.Reports;
using BookKiosk.CMS.ViewModels.Reports;

namespace BookKiosk.CMS.Helpers
{
    public enum MonthlyRevenueValidationStatus
    {
        Valid,
        InvalidContract,
        Unavailable
    }

    public class MonthlyRevenueNormalizationResult
    {
        public MonthlyRevenueValidationStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public List<int> DuplicateMonths { get; set; } = new();
        public List<int> OutOfRangeMonths { get; set; } = new();
        public List<MonthlyReportRow> Rows { get; set; } = new();
        public decimal TotalAnnualRevenue { get; set; }
        public int TotalAnnualOrders { get; set; }

        public decimal AnnualAverageOrderValue => TotalAnnualOrders > 0
            ? Math.Round(TotalAnnualRevenue / TotalAnnualOrders)
            : 0m;

        public bool IsValid => Status == MonthlyRevenueValidationStatus.Valid;
    }

    /// <summary>
    /// REVENUE-MONTHLY-01: Bộ chuẩn hóa và kiểm thực hợp đồng dữ liệu doanh thu 12 tháng dùng chung cho CMS.
    /// Quy tắc:
    /// 1. Tối đa 1 dòng/tháng (tháng 1..12). Tháng trùng hoặc tháng ngoài 1..12 => InvalidContract (TỪ CHỐI toan bộ, KHÔNG GroupBy+Sum).
    /// 2. Tháng thiếu => Điền 0 (Zero-fill) đủ 12 tháng từ tháng 1 đến 12.
    /// 3. Tổng năm tính từ CÙNG bộ 12 tháng chuẩn hóa.
    /// </summary>
    public static class MonthlyRevenueNormalizer
    {
        public static MonthlyRevenueNormalizationResult Normalize(IEnumerable<MonthlyRevenueItemDto>? rawItems, int? year = null)
        {
            if (rawItems == null)
            {
                return new MonthlyRevenueNormalizationResult
                {
                    Status = MonthlyRevenueValidationStatus.Unavailable,
                    ErrorMessage = "Dữ liệu báo cáo doanh thu không khả dụng từ máy chủ."
                };
            }

            var itemsList = rawItems.ToList();

            // Kịch bản rỗng hợp lệ (ví dụ năm chưa có đơn hàng nào)
            if (itemsList.Count == 0)
            {
                var emptyRows = new List<MonthlyReportRow>(12);
                for (int m = 1; m <= 12; m++)
                {
                    emptyRows.Add(new MonthlyReportRow
                    {
                        Month = m,
                        Revenue = 0m,
                        TotalOrders = 0
                    });
                }

                return new MonthlyRevenueNormalizationResult
                {
                    Status = MonthlyRevenueValidationStatus.Valid,
                    Rows = emptyRows,
                    TotalAnnualRevenue = 0m,
                    TotalAnnualOrders = 0
                };
            }

            // 1. Kiểm tra tháng ngoài khoảng 1..12
            var outOfRange = itemsList
                .Where(m => m.Month < 1 || m.Month > 12)
                .Select(m => m.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            if (outOfRange.Any())
            {
                return new MonthlyRevenueNormalizationResult
                {
                    Status = MonthlyRevenueValidationStatus.InvalidContract,
                    OutOfRangeMonths = outOfRange,
                    ErrorMessage = $"Hợp đồng doanh thu không hợp lệ: Phát hiện tháng nằm ngoài khoảng 1..12 ({string.Join(", ", outOfRange)})."
                };
            }

            // 2. Kiểm tra giá trị âm
            var hasNegative = itemsList.Any(m => m.Revenue < 0 || m.TotalOrders < 0);
            if (hasNegative)
            {
                return new MonthlyRevenueNormalizationResult
                {
                    Status = MonthlyRevenueValidationStatus.InvalidContract,
                    ErrorMessage = "Hợp đồng doanh thu không hợp lệ: Doanh thu hoặc số đơn hàng không được là số âm."
                };
            }

            // 3. Kiểm tra tháng trùng lặp (TUYỆT ĐỐI KHÔNG GroupBy + Sum để che giấu vi phạm)
            var duplicates = itemsList
                .GroupBy(m => m.Month)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(m => m)
                .ToList();

            if (duplicates.Any())
            {
                return new MonthlyRevenueNormalizationResult
                {
                    Status = MonthlyRevenueValidationStatus.InvalidContract,
                    DuplicateMonths = duplicates,
                    ErrorMessage = $"Hợp đồng doanh thu không hợp lệ: Phát hiện dữ liệu trùng lặp tháng ({string.Join(", ", duplicates)})."
                };
            }

            // 4. Chuẩn hóa đủ 12 tháng duy nhất (Zero-fill cho các tháng thiếu)
            var dict = itemsList.ToDictionary(m => m.Month);
            var normalizedRows = new List<MonthlyReportRow>(12);
            decimal totalRevenue = 0m;
            int totalOrders = 0;

            for (int m = 1; m <= 12; m++)
            {
                if (dict.TryGetValue(m, out var item))
                {
                    normalizedRows.Add(new MonthlyReportRow
                    {
                        Month = m,
                        Revenue = item.Revenue,
                        TotalOrders = item.TotalOrders
                    });
                    totalRevenue += item.Revenue;
                    totalOrders += item.TotalOrders;
                }
                else
                {
                    normalizedRows.Add(new MonthlyReportRow
                    {
                        Month = m,
                        Revenue = 0m,
                        TotalOrders = 0
                    });
                }
            }

            return new MonthlyRevenueNormalizationResult
            {
                Status = MonthlyRevenueValidationStatus.Valid,
                Rows = normalizedRows,
                TotalAnnualRevenue = totalRevenue,
                TotalAnnualOrders = totalOrders
            };
        }
    }
}
