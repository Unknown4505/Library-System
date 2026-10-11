namespace BookKiosk.Application.DTOs.Reports;

public sealed class RevenueReportDto
{
    public int Year { get; set; }
    public List<MonthlyRevenueDto> Data { get; set; } = new();
}

public sealed class MonthlyRevenueDto
{
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public int TotalOrders { get; set; }
}

public sealed class TopSellingBookDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public sealed class KioskStatusSummaryDto
{
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Error { get; set; }
    public int OpenIncidents { get; set; }
}
