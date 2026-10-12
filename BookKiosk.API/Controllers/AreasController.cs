using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AreasController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AreasController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách các khu vực/kệ sách (Dùng để hiển thị vị trí sách trên Kiosk)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAreas()
    {
        var areas = await _context.Areas
            .OrderBy(a => a.Name)
            .Select(a => new AreaDto
            {
                AreaId = a.AreaId,
                Name = a.Name,
                MapCoordinates = a.MapCoordinates
            })
            .ToListAsync();
            
        return Ok(ApiResponseDto<IEnumerable<AreaDto>>.Ok(areas));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateArea([FromBody] CreateAreaDto request)
    {
        var name = request.Name.Trim();
        if (await _context.Areas.AnyAsync(a => a.Name == name))
            return Conflict(ApiResponseDto<object>.Error("DUPLICATE_NAME", "Tên khu vực đã tồn tại."));

        var area = new Area
        {
            Name = name,
            MapCoordinates = request.MapCoordinates?.Trim()
        };
        _context.Areas.Add(area);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ApiResponseDto<AreaDto>.Ok(ToDto(area), "Tạo khu vực thành công."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateArea(int id, [FromBody] UpdateAreaDto request)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null)
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy khu vực."));

        var name = request.Name.Trim();
        if (await _context.Areas.AnyAsync(a => a.AreaId != id && a.Name == name))
            return Conflict(ApiResponseDto<object>.Error("DUPLICATE_NAME", "Tên khu vực đã tồn tại."));

        area.Name = name;
        area.MapCoordinates = request.MapCoordinates?.Trim();
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<AreaDto>.Ok(ToDto(area), "Cập nhật khu vực thành công."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteArea(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null)
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy khu vực."));

        _context.Areas.Remove(area);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<bool>.Ok(true, "Xóa khu vực thành công; sách liên quan được bỏ vị trí kệ."));
    }

    private static AreaDto ToDto(Area area) => new()
    {
        AreaId = area.AreaId,
        Name = area.Name,
        MapCoordinates = area.MapCoordinates
    };
}
