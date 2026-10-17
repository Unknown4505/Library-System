using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Inventory;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetSuppliers()
    {
        var suppliers = await _supplierService.GetSuppliersAsync();
        return Ok(ApiResponseDto<IEnumerable<SupplierDto>>.Ok(suppliers, "Lấy danh sách nhà cung cấp thành công."));
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetSupplier(int id)
    {
        try
        {
            var supplier = await _supplierService.GetSupplierByIdAsync(id);
            return Ok(ApiResponseDto<SupplierDto>.Ok(supplier, "Lấy thông tin nhà cung cấp thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierDto dto)
    {
        try
        {
            var result = await _supplierService.CreateSupplierAsync(dto);
            return Ok(ApiResponseDto<SupplierDto>.Ok(result, "Tạo mới nhà cung cấp thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateSupplier(int id, [FromBody] UpdateSupplierDto dto)
    {
        try
        {
            var result = await _supplierService.UpdateSupplierAsync(id, dto);
            return Ok(ApiResponseDto<SupplierDto>.Ok(result, "Cập nhật nhà cung cấp thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteSupplier(int id)
    {
        try
        {
            await _supplierService.DeleteSupplierAsync(id);
            return Ok(ApiResponseDto<bool>.Ok(true, "Xóa nhà cung cấp thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
    }
}
