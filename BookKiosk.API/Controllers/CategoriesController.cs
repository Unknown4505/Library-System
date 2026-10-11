using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách tất cả các danh mục sách (Dùng cho dropdown/filter ở Kiosk và CMS)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Description = c.Description
            })
            .ToListAsync();
            
        return Ok(ApiResponseDto<IEnumerable<CategoryDto>>.Ok(categories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto request)
    {
        var name = request.Name.Trim();
        if (await _context.Categories.AnyAsync(c => c.Name == name))
            return Conflict(ApiResponseDto<object>.Error("DUPLICATE_NAME", "Tên danh mục đã tồn tại."));

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim()
        };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ApiResponseDto<CategoryDto>.Ok(ToDto(category), "Tạo danh mục thành công."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDto request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy danh mục."));

        var name = request.Name.Trim();
        if (await _context.Categories.AnyAsync(c => c.CategoryId != id && c.Name == name))
            return Conflict(ApiResponseDto<object>.Error("DUPLICATE_NAME", "Tên danh mục đã tồn tại."));

        category.Name = name;
        category.Description = request.Description?.Trim();
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<CategoryDto>.Ok(ToDto(category), "Cập nhật danh mục thành công."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy danh mục."));

        if (await _context.Books.AnyAsync(b => b.CategoryId == id))
            return Conflict(ApiResponseDto<object>.Error("CATEGORY_IN_USE", "Không thể xóa danh mục đang được sách sử dụng."));

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<bool>.Ok(true, "Xóa danh mục thành công."));
    }

    private static CategoryDto ToDto(Category category) => new()
    {
        CategoryId = category.CategoryId,
        Name = category.Name,
        Description = category.Description
    };
}
