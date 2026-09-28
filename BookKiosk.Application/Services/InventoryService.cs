using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository _inventoryRepository;

    public InventoryService(IUnitOfWork unitOfWork, IInventoryRepository inventoryRepository)
    {
        _unitOfWork = unitOfWork;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<ApiResponseDto<object>> ImportStockAsync(ImportReceipt receipt)
    {
        if (receipt.ImportReceiptDetails == null || !receipt.ImportReceiptDetails.Any())
            return ApiResponseDto<object>.Error("BAD_REQUEST", "Phiếu nhập kho không có chi tiết sách.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            receipt.CreatedAt = DateTime.Now;
            if (receipt.UserId == 0) receipt.UserId = 1;

            await _inventoryRepository.AddImportReceiptAsync(receipt);
            await _unitOfWork.SaveChangesAsync(); 

            foreach (var detail in receipt.ImportReceiptDetails)
            {
                var book = await _inventoryRepository.GetBookByIdAsync(detail.BookId);
                if (book != null)
                {
                    book.StockQuantity += detail.Quantity;
                    if (detail.CostPrice > 0)
                    {
                        book.CostPrice = detail.CostPrice;
                    }
                }
                else
                {
                    throw new Exception($"Sách có ID {detail.BookId} không tồn tại.");
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return ApiResponseDto<object>.Ok(new { Message = "Nhập kho thành công", ReceiptId = receipt.ImportReceiptId });
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            return ApiResponseDto<object>.Error("INTERNAL_ERROR", $"Lỗi khi nhập kho: {ex.Message}");
        }
    }
}
