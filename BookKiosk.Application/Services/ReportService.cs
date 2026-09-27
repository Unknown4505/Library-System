using BookKiosk.Application.DTOs.Common;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IReportService
{
    Task<ApiResponseDto<object>> GetRevenueByMonthAsync(int year);
    Task<ApiResponseDto<object>> GetTopSellingBooksAsync(int top);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<object>> GetRevenueByMonthAsync(int year)
    {
        if (year <= 0) year = DateTime.Now.Year;

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

        var result = Enumerable.Range(1, 12).Select(m => new
        {
            Month = m,
            Revenue = rawData.FirstOrDefault(d => d.Month == m)?.Revenue ?? 0,
            TotalOrders = rawData.FirstOrDefault(d => d.Month == m)?.TotalOrders ?? 0
        }).ToList();

        return ApiResponseDto<object>.Ok(new { Year = year, Data = result });
    }

    public async Task<ApiResponseDto<object>> GetTopSellingBooksAsync(int top)
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

        return ApiResponseDto<object>.Ok(topBooks);
    }
}
