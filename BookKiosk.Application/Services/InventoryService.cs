using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Inventory;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IInventoryService
{
    Task<ApiResponseDto<object>> ImportStockAsync(ImportReceipt receipt);
    Task<IEnumerable<StockItemDto>> GetStockAsync(bool lowOnly = false, string? keyword = null);
    Task<bool> AdjustStockAsync(AdjustStockDto dto, int userId);
    Task<PaginatedResultDto<ImportReceiptDto>> GetReceiptsAsync(int? supplierId, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    Task<ImportReceiptDetailDto> GetReceiptByIdAsync(int id);
    Task<int> CreateReceiptAsync(CreateImportReceiptDto dto, int userId);
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
            receipt.CreatedAt = DateTime.UtcNow;
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

    public async Task<IEnumerable<StockItemDto>> GetStockAsync(bool lowOnly = false, string? keyword = null)
    {
        var books = await _inventoryRepository.GetStockAsync(lowOnly, keyword);
        return books.Select(b => new StockItemDto
        {
            BookId = b.BookId,
            Barcode = b.Barcode,
            Title = b.Title,
            Author = b.Author,
            ImageUrl = b.ImageUrl,
            CategoryName = b.Category?.Name ?? string.Empty,
            AreaName = b.Area?.Name,
            StockQuantity = b.StockQuantity,
            ReservedQuantity = b.ReservedQuantity,
            AvailableStock = Math.Max(0, b.StockQuantity - b.ReservedQuantity),
            CostPrice = b.CostPrice,
            SellingPrice = b.SellingPrice,
            IsActive = b.IsActive
        }).ToList();
    }

    public async Task<bool> AdjustStockAsync(AdjustStockDto dto, int userId)
    {
        if (dto.ActualQuantity < 0)
            throw new ArgumentException("Số lượng tồn thực tế phải lớn hơn hoặc bằng 0.");

        var book = await _inventoryRepository.GetBookByIdAsync(dto.BookId);
        if (book == null)
            throw new KeyNotFoundException($"Không tìm thấy sách với ID {dto.BookId}.");

        if (dto.ActualQuantity < book.ReservedQuantity)
        {
            throw new InvalidOperationException($"Số lượng tồn kho thực tế ({dto.ActualQuantity}) không thể nhỏ hơn số lượng đang giữ chỗ ({book.ReservedQuantity}) cho các đơn hàng chưa hoàn tất. Vui lòng đợi các đơn hàng được xử lý hoặc hủy trước khi điều chỉnh.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var updated = await _inventoryRepository.AdjustStockAsync(dto.BookId, dto.ActualQuantity);
            if (!updated)
                throw new InvalidOperationException("Không thể cập nhật số lượng tồn kho.");

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<PaginatedResultDto<ImportReceiptDto>> GetReceiptsAsync(int? supplierId, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var (receipts, totalCount) = await _inventoryRepository.GetReceiptsPagedAsync(
            supplierId,
            fromDate,
            toDate,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100));

        var items = receipts.Select(r => new ImportReceiptDto
        {
            ImportReceiptId = r.ImportReceiptId,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name ?? string.Empty,
            UserId = r.UserId,
            CreatedByName = r.CreatedByUser?.FullName,
            CreatedAt = r.CreatedAt,
            TotalAmount = r.TotalAmount,
            Note = r.Note,
            ItemCount = r.ImportReceiptDetails.Count,
            TotalQuantity = r.ImportReceiptDetails.Sum(d => d.Quantity)
        }).ToList();

        return new PaginatedResultDto<ImportReceiptDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ImportReceiptDetailDto> GetReceiptByIdAsync(int id)
    {
        var r = await _inventoryRepository.GetReceiptDetailAsync(id);
        if (r == null)
            throw new KeyNotFoundException($"Không tìm thấy phiếu nhập kho ID {id}.");

        return new ImportReceiptDetailDto
        {
            ImportReceiptId = r.ImportReceiptId,
            SupplierId = r.SupplierId,
            SupplierName = r.Supplier?.Name ?? string.Empty,
            SupplierPhone = r.Supplier?.PhoneNumber,
            UserId = r.UserId,
            CreatedByName = r.CreatedByUser?.FullName,
            CreatedAt = r.CreatedAt,
            TotalAmount = r.TotalAmount,
            Note = r.Note,
            Details = r.ImportReceiptDetails.Select(d => new ImportReceiptItemDto
            {
                BookId = d.BookId,
                Barcode = d.Book?.Barcode ?? string.Empty,
                Title = d.Book?.Title ?? string.Empty,
                Author = d.Book?.Author,
                ImageUrl = d.Book?.ImageUrl,
                Quantity = d.Quantity,
                CostPrice = d.CostPrice
            }).ToList()
        };
    }

    public async Task<int> CreateReceiptAsync(CreateImportReceiptDto dto, int userId)
    {
        var items = dto.Items != null && dto.Items.Any() ? dto.Items : dto.Details;
        if (items == null || !items.Any())
            throw new ArgumentException("Phiếu nhập phải có ít nhất 1 đầu sách.");

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Số lượng sách (ID: {item.BookId}) phải lớn hơn 0.");
            var price = item.CostPrice > 0 ? item.CostPrice : item.ImportPrice;
            if (price < 0)
                throw new ArgumentException($"Giá nhập sách (ID: {item.BookId}) không được âm.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            decimal totalAmount = items.Sum(i => i.Quantity * (i.CostPrice > 0 ? i.CostPrice : i.ImportPrice));

            var receipt = new ImportReceipt
            {
                SupplierId = dto.SupplierId,
                UserId = userId > 0 ? userId : 1,
                Note = dto.Note ?? dto.Notes,
                TotalAmount = totalAmount,
                CreatedAt = DateTime.UtcNow
            };

            await _inventoryRepository.AddImportReceiptAsync(receipt);
            await _unitOfWork.SaveChangesAsync();

            foreach (var item in items)
            {
                var book = await _inventoryRepository.GetBookByIdAsync(item.BookId);
                if (book == null)
                    throw new KeyNotFoundException($"Không tìm thấy sách với ID {item.BookId}.");

                var price = item.CostPrice > 0 ? item.CostPrice : item.ImportPrice;

                book.StockQuantity += item.Quantity;
                if (price > 0)
                {
                    book.CostPrice = price;
                }

                receipt.ImportReceiptDetails.Add(new ImportReceiptDetail
                {
                    ImportReceiptId = receipt.ImportReceiptId,
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    CostPrice = price
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return receipt.ImportReceiptId;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
