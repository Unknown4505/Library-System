using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PromotionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PromotionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetPromotions()
    {
        var promotions = await _context.Promotions
            .Include(p => p.OrderDiscount)
            .OrderByDescending(p => p.PromotionId)
            .ToListAsync();
        return Ok(promotions);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion([FromBody] Promotion promotion)
    {
        _context.Promotions.Add(promotion);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPromotions), new { id = promotion.PromotionId }, promotion);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] Promotion updated)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return NotFound();

        promotion.Name = updated.Name;
        promotion.Description = updated.Description;
        promotion.StartDate = updated.StartDate;
        promotion.EndDate = updated.EndDate;
        promotion.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        return Ok(promotion);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var promotion = await _context.Promotions.FindAsync(id);
        if (promotion == null) return NotFound();

        // Không nên xóa cứng nếu đã có Order áp dụng KM này, thay vào đó là Soft Delete (set IsActive = false)
        promotion.IsActive = false;
        
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
