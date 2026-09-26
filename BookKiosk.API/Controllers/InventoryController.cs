using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public InventoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// API Nhập kho sách
    /// Tự động cộng số lượng vật lý vào StockQuantity của Books
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportStock([FromBody] ImportReceipt receipt)
    {
        if (receipt.ImportReceiptDetails == null || !receipt.ImportReceiptDetails.Any())
            return BadRequest("Phiếu nhập kho không có chi tiết sách.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Set thời gian và người tạo (mock UserId cho CMS Admin)
            receipt.ImportDate = DateTime.Now;
            if (receipt.UserId == 0) receipt.UserId = 1;

            _context.ImportReceipts.Add(receipt);
            await _context.SaveChangesAsync(); // Lưu để lấy ReceiptId

            // Cộng tồn kho cho từng cuốn sách
            foreach (var detail in receipt.ImportReceiptDetails)
            {
                var book = await _context.Books.FindAsync(detail.BookId);
                if (book != null)
                {
                    // Update số lượng tồn kho vật lý
                    book.StockQuantity += detail.Quantity;
                    
                    // Nếu cần thiết cập nhật luôn cả giá nhập mới nhất
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

            return Ok(new { Message = "Nhập kho thành công", ReceiptId = receipt.ReceiptId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { Message = "Lỗi khi nhập kho.", Error = ex.Message });
        }
    }
}
