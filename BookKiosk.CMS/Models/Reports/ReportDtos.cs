using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Reports
{
    public class RevenueReportResponseDto
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("data")]
        public List<MonthlyRevenueItemDto> MonthlyData { get; set; } = new();
    }

    public class MonthlyRevenueItemDto
    {
        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("revenue")]
        public decimal Revenue { get; set; }

        [JsonPropertyName("totalOrders")]
        public int TotalOrders { get; set; }
    }

    public class TopSellingBookDto
    {
        [JsonPropertyName("bookId")]
        public int BookId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("totalSold")]
        public int TotalSold { get; set; }

        [JsonPropertyName("totalRevenue")]
        public decimal TotalRevenue { get; set; }
    }
}
