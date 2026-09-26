using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Thống kê doanh thu theo từng tháng trong năm (Chỉ tính đơn hàng đã thanh toán - Paid).
    /// </summary>
    /// <param name="year">Năm cần thống kê</param>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueByMonth([FromQuery] int year)
    {
        if (year <= 0) year = DateTime.Now.Year;

        // Group dữ liệu theo tháng
        var rawData = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatus.Paid && o.CompletedAt.HasValue && o.CompletedAt.Value.Year == year)
            .GroupBy(o => o.CompletedAt.Value.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Sum(o => o.TotalAmount),
                TotalOrders = g.Count()
            })
            .ToListAsync();

        // Chuẩn hóa dữ liệu trả về đủ 12 tháng
        var result = Enumerable.Range(1, 12).Select(m => new
        {
            Month = m,
            Revenue = rawData.FirstOrDefault(d => d.Month == m)?.Revenue ?? 0,
            TotalOrders = rawData.FirstOrDefault(d => d.Month == m)?.TotalOrders ?? 0
        }).ToList();

        return Ok(new { Year = year, Data = result });
    }

    /// <summary>
    /// Thống kê top sách bán chạy nhất (dựa trên số lượng đã bán trong đơn hàng Paid).
    /// </summary>
    /// <param name="top">Số lượng sách cần lấy</param>
    [HttpGet("top-books")]
    public async Task<IActionResult> GetTopSellingBooks([FromQuery] int top = 10)
    {
        var topBooks = await _context.OrderDetails
            .Include(od => od.Order)
            .Include(od => od.Book)
            .Where(od => od.Order != null && od.Order.OrderStatus == OrderStatus.Paid)
            .GroupBy(od => new { od.BookId, od.Book!.Title, od.Book.ImageUrl })
            .Select(g => new
            {
                BookId = g.Key.BookId,
                Title = g.Key.Title,
                ImageUrl = g.Key.ImageUrl,
                TotalSold = g.Sum(od => od.Quantity),
                TotalRevenue = g.Sum(od => od.TotalPrice)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(top)
            .ToListAsync();

        return Ok(topBooks);
    }
}
