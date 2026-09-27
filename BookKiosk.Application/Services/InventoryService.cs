using BookKiosk.Application.DTOs.Common;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IInventoryService
{
    Task<ApiResponseDto<object>> ImportStockAsync(ImportReceipt receipt);
}

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponseDto<object>> ImportStockAsync(ImportReceipt receipt)
    {
        if (receipt.ImportReceiptDetails == null || !receipt.ImportReceiptDetails.Any())
            return ApiResponseDto<object>.Error("BAD_REQUEST", "Phiếu nhập kho không có chi tiết sách.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            receipt.ImportDate = DateTime.Now;
            if (receipt.UserId == 0) receipt.UserId = 1;

            _context.ImportReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            foreach (var detail in receipt.ImportReceiptDetails)
            {
                var book = await _context.Books.FindAsync(detail.BookId);
                if (book != null)
                {
                    book.StockQuantity += detail.Quantity;
                    if (detail.ImportPrice > 0)
                    {
                        book.CostPrice = detail.ImportPrice;
                    }
                }
                else
                {
                    throw new Exception($"Sách có ID {detail.BookId} không tồn tại.");
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return ApiResponseDto<object>.Ok(new { Message = "Nhập kho thành công", ReceiptId = receipt.ReceiptId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return ApiResponseDto<object>.Error("INTERNAL_ERROR", $"Lỗi khi nhập kho: {ex.Message}");
        }
    }
}
