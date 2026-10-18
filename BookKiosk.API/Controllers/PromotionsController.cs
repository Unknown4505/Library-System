using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Promotions;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetPromotions()
    {
        return Ok(await _promotionService.GetPromotionsAsync());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionDto request)
    {
        return Ok(await _promotionService.CreatePromotionAsync(request));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] PromotionDto request)
    {
        var result = await _promotionService.UpdatePromotionAsync(id, request);
        if (!result.Success && result.Code == "NOT_FOUND")
            return NotFound(result);
        return Ok(result);
    }

    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TogglePromotionActive(int id, [FromBody] TogglePromotionActiveDto request)
    {
        if (request == null)
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Dữ liệu yêu cầu không hợp lệ."));

        var result = await _promotionService.TogglePromotionActiveAsync(id, request.IsActive);
        if (!result.Success)
        {
            if (result.Code == "NOT_FOUND")
                return NotFound(result);
            if (result.Code == "CONFLICT")
                return Conflict(result);
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var result = await _promotionService.DeletePromotionAsync(id);
        if (!result.Success && result.Code == "NOT_FOUND")
            return NotFound(result);
        return Ok(result);
    }
}
