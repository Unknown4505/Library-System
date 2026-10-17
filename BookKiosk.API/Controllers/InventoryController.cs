using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Inventory;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("stock")]
    public async Task<IActionResult> GetStock([FromQuery] bool lowOnly = false, [FromQuery] string? keyword = null)
    {
        var stock = await _inventoryService.GetStockAsync(lowOnly, keyword);
        return Ok(ApiResponseDto<IEnumerable<StockItemDto>>.Ok(stock, "Lấy danh sách tồn kho thành công."));
    }

    [HttpPost("adjust")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockDto dto)
    {
        try
        {
            int userId = GetCurrentUserId();
            var success = await _inventoryService.AdjustStockAsync(dto, userId);
            return Ok(ApiResponseDto<bool>.Ok(success, "Điều chỉnh kiểm kê tồn kho thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpGet("receipts")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] int? supplierId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var receipts = await _inventoryService.GetReceiptsAsync(supplierId, fromDate, toDate, page, pageSize);
        return Ok(ApiResponseDto<PaginatedResultDto<ImportReceiptDto>>.Ok(receipts, "Lấy danh sách phiếu nhập kho thành công."));
    }

    [HttpGet("receipts/{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetReceiptById(int id)
    {
        try
        {
            var receipt = await _inventoryService.GetReceiptByIdAsync(id);
            return Ok(ApiResponseDto<ImportReceiptDetailDto>.Ok(receipt, "Lấy thông tin chi tiết phiếu nhập kho thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
    }

    [HttpPost("receipts")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateImportReceiptDto dto)
    {
        try
        {
            int userId = GetCurrentUserId();
            var receiptId = await _inventoryService.CreateReceiptAsync(dto, userId);
            return Ok(ApiResponseDto<int>.Ok(receiptId, "Tạo phiếu nhập kho thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponseDto<object>.Error("SERVER_ERROR", $"Lỗi tạo phiếu nhập kho: {ex.Message}"));
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportStock([FromBody] ImportReceipt receipt)
    {
        return Ok(await _inventoryService.ImportStockAsync(receipt));
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && int.TryParse(claim.Value, out int userId))
        {
            return userId;
        }
        return 1;
    }
}
