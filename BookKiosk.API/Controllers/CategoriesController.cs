using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
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
            .Select(c => new 
            {
                c.CategoryId,
                c.Name,
                c.Description
            })
            .ToListAsync();
            
        return Ok(categories);
    }
}
