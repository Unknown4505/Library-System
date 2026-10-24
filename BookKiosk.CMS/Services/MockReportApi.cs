using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Reports;

namespace BookKiosk.CMS.Services
{
    /// <summary>
    /// MockReportApi: Giả lập IReportApi phục vụ kiểm thử và chạy offline khi bật cờ Cms:UseMockApi=true trong Development.
    /// Cung cấp dữ liệu chuẩn 12 tháng cho Task 11.3 và Top 10 sách bán chạy.
    /// CẢNH BÁO: Bị cấm tuyệt đối trên môi trường Production/Release.
    /// </summary>
    public class MockReportApi : IReportApi
    {
        private readonly List<TopSellingBookDto> _topBooks = new()
        {
            new TopSellingBookDto { BookId = 1, Title = "Đắc Nhân Tâm - Dale Carnegie", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Dac+Nhan+Tam", TotalSold = 312, TotalRevenue = 26_832_000m },
            new TopSellingBookDto { BookId = 2, Title = "Nhà Giả Kim - Paulo Coelho", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Nha+Gia+Kim", TotalSold = 285, TotalRevenue = 22_515_000m },
            new TopSellingBookDto { BookId = 3, Title = "Tuổi Trẻ Đáng Giá Bao Nhiêu - Rosie Nguyễn", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Tuoi+Tre", TotalSold = 240, TotalRevenue = 19_200_000m },
            new TopSellingBookDto { BookId = 4, Title = "Cây Cam Ngọt Của Tôi - José Mauro", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Cay+Cam+Ngot", TotalSold = 215, TotalRevenue = 23_220_000m },
            new TopSellingBookDto { BookId = 5, Title = "Tâm Lý Học Về Tiền - Morgan Housel", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Tam+Ly+Hoc", TotalSold = 198, TotalRevenue = 37_422_000m },
            new TopSellingBookDto { BookId = 6, Title = "Hành Tinh Của Một Kẻ Nghĩ Nhiều - Nguyễn Đoàn Minh Thư", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Hanh+Tinh", TotalSold = 175, TotalRevenue = 15_050_000m },
            new TopSellingBookDto { BookId = 7, Title = "Muôn Kiếp Nhân Sinh (Tập 1) - Nguyên Phong", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Muon+Kiep", TotalSold = 160, TotalRevenue = 26_880_000m },
            new TopSellingBookDto { BookId = 8, Title = "Atomic Habits - Thay Đổi Tí Hon - James Clear", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Atomic+Habits", TotalSold = 145, TotalRevenue = 27_405_000m },
            new TopSellingBookDto { BookId = 9, Title = "Sapiens: Lược Sử Loài Người - Yuval Noah Harari", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Sapiens", TotalSold = 130, TotalRevenue = 37_570_000m },
            new TopSellingBookDto { BookId = 10, Title = "Hiểu Về Trái Tim - Minh Niệm", ImageUrl = "https://placehold.co/100x140/f8f9fa/6c757d?text=Hieu+Ve+Trai+Tim", TotalSold = 115, TotalRevenue = 18_170_000m }
        };

        public Task<ApiResult<RevenueReportResponseDto>> GetRevenueByMonthAsync(int year)
        {
            // Doanh thu mô phỏng 12 tháng theo mùa vụ (cuối năm và hè cao điểm)
            var monthlyRevenues = new[]
            {
                (Month: 1, Revenue: 45_000_000m, Orders: 180),
                (Month: 2, Revenue: 52_000_000m, Orders: 210),
                (Month: 3, Revenue: 48_500_000m, Orders: 195),
                (Month: 4, Revenue: 61_000_000m, Orders: 245),
                (Month: 5, Revenue: 58_000_000m, Orders: 230),
                (Month: 6, Revenue: 72_000_000m, Orders: 290),
                (Month: 7, Revenue: 68_500_000m, Orders: 275),
                (Month: 8, Revenue: 75_000_000m, Orders: 305),
                (Month: 9, Revenue: 84_000_000m, Orders: 330),
                (Month: 10, Revenue: 91_500_000m, Orders: 360),
                (Month: 11, Revenue: 69_000_000m, Orders: 270),
                (Month: 12, Revenue: 105_000_000m, Orders: 420)
            };

            var items = monthlyRevenues.Select(m => new MonthlyRevenueItemDto
            {
                Month = m.Month,
                Revenue = m.Revenue,
                TotalOrders = m.Orders
            }).ToList();

            var response = new RevenueReportResponseDto
            {
                Year = year,
                MonthlyData = items
            };

            return Task.FromResult(ApiResult<RevenueReportResponseDto>.Ok(response));
        }

        public Task<ApiResult<IEnumerable<TopSellingBookDto>>> GetTopSellingBooksAsync(int top = 10)
        {
            var result = _topBooks.Take(top).ToList();
            return Task.FromResult(ApiResult<IEnumerable<TopSellingBookDto>>.Ok(result));
        }
    }
}
