using BookKiosk.Application.DTOs.Promotions;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPromotions()
    {
        return Ok(await _promotionService.GetPromotionsAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionDto request)
    {
        return Ok(await _promotionService.CreatePromotionAsync(request));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] PromotionDto request)
    {
        return Ok(await _promotionService.UpdatePromotionAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        return Ok(await _promotionService.DeletePromotionAsync(id));
    }
}
