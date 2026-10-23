using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Infrastructure.Repositories;

public class KioskAdvertisingRepository : IKioskAdvertisingRepository
{
    private readonly ApplicationDbContext _context;

    public KioskAdvertisingRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<AdvertisingPromotionDto>> GetActivePromotionsAsync(DateTime utcNow)
    {
        var rawList = await _context.Promotions
            .AsNoTracking()
            .Where(p => p.IsActive 
                        && p.StartDate <= utcNow 
                        && utcNow <= p.EndDate
                        && p.PromotionType == PromotionType.OrderDiscount 
                        && p.OrderDiscount != null)
            .OrderBy(p => p.EndDate)
            .ThenBy(p => p.PromotionId)
            .Select(p => new AdvertisingPromotionDto
            {
                PromotionId = p.PromotionId,
                Name = p.Name,
                Description = p.Description,
                PromotionType = p.PromotionType,
                OrderDiscount = new AdvertisingOrderDiscountDto
                {
                    MinOrderValue = p.OrderDiscount!.MinOrderValue,
                    DiscountAmount = p.OrderDiscount!.DiscountAmount
                },
                ProductDiscounts = new List<AdvertisingProductDiscountDto>(),
                StartDate = p.StartDate,
                EndDate = p.EndDate
            })
            .ToListAsync();

        // Chuẩn hóa DateTimeKind.Utc để JSON serialize luôn ra ISO 8601 có hậu tố 'Z'
        return rawList.Select(p => new AdvertisingPromotionDto
        {
            PromotionId = p.PromotionId,
            Name = p.Name,
            Description = p.Description,
            PromotionType = p.PromotionType,
            OrderDiscount = p.OrderDiscount,
            ProductDiscounts = p.ProductDiscounts,
            StartDate = DateTime.SpecifyKind(p.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(p.EndDate, DateTimeKind.Utc)
        }).ToList();
    }

    public async Task<List<AdvertisingNewArrivalDto>> GetNewArrivalsAsync(DateTime utcNow, int limit = 10)
    {
        var cutoff = utcNow.AddDays(-30);

        var rawList = await _context.Books
            .AsNoTracking()
            .Where(b => b.IsActive && (b.StockQuantity - b.ReservedQuantity) > 0)
            .Select(b => new
            {
                Book = b,
                LastImportedAt = _context.ImportReceiptDetails
                    .Where(d => d.BookId == b.BookId 
                                && d.ImportReceipt != null 
                                && d.ImportReceipt.CreatedAt >= cutoff 
                                && d.ImportReceipt.CreatedAt <= utcNow)
                    .Max(d => (DateTime?)d.ImportReceipt!.CreatedAt)
            })
            .Where(x => x.LastImportedAt != null)
            .OrderByDescending(x => x.LastImportedAt)
            .ThenBy(x => x.Book.BookId)
            .Take(limit)
            .Select(x => new AdvertisingNewArrivalDto
            {
                BookId = x.Book.BookId,
                Title = x.Book.Title,
                ImageUrl = x.Book.ImageUrl,
                SellingPrice = x.Book.SellingPrice,
                AvailableStock = x.Book.StockQuantity - x.Book.ReservedQuantity,
                LastImportedAt = x.LastImportedAt!.Value
            })
            .ToListAsync();

        // Chuẩn hóa DateTimeKind.Utc để JSON serialize có hậu tố 'Z'
        return rawList.Select(x => new AdvertisingNewArrivalDto
        {
            BookId = x.BookId,
            Title = x.Title,
            ImageUrl = x.ImageUrl,
            SellingPrice = x.SellingPrice,
            AvailableStock = x.AvailableStock,
            LastImportedAt = DateTime.SpecifyKind(x.LastImportedAt, DateTimeKind.Utc)
        }).ToList();
    }

    public async Task<List<AdvertisingBestSellerDto>> GetBestSellersAsync(DateTime utcNow, int limit = 10)
    {
        var cutoff = utcNow.AddDays(-30);

        return await _context.Books
            .AsNoTracking()
            .Where(b => b.IsActive && (b.StockQuantity - b.ReservedQuantity) > 0)
            .Select(b => new
            {
                Book = b,
                TotalSold = _context.OrderDetails
                    .Where(od => od.BookId == b.BookId 
                                 && od.Order != null 
                                 && od.Order.OrderStatus == OrderStatus.Paid 
                                 && od.Order.CompletedAt.HasValue 
                                 && od.Order.CompletedAt.Value >= cutoff
                                 && od.Order.CompletedAt.Value <= utcNow)
                    .Sum(od => (int?)od.Quantity) ?? 0
            })
            .Where(x => x.TotalSold > 0)
            .OrderByDescending(x => x.TotalSold)
            .ThenBy(x => x.Book.BookId)
            .Take(limit)
            .Select(x => new AdvertisingBestSellerDto
            {
                BookId = x.Book.BookId,
                Title = x.Book.Title,
                ImageUrl = x.Book.ImageUrl,
                SellingPrice = x.Book.SellingPrice,
                AvailableStock = x.Book.StockQuantity - x.Book.ReservedQuantity,
                TotalSold = x.TotalSold
            })
            .ToListAsync();
    }
}
