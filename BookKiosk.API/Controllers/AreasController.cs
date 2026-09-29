using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
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
            .Select(a => new 
            {
                a.AreaId,
                a.Name,
                a.Description
            })
            .ToListAsync();
            
        return Ok(areas);
    }
}
