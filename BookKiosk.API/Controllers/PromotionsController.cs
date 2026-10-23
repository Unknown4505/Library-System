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

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetPromotionById(int id)
    {
        var result = await _promotionService.GetPromotionByIdAsync(id);
        if (!result.Success && result.Code == "NOT_FOUND")
            return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionDto request)
    {
        var result = await _promotionService.CreatePromotionAsync(request);
        if (!result.Success)
        {
            if (result.Code == "BAD_REQUEST")
                return BadRequest(result);
            return StatusCode(500, result);
        }
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePromotion(int id, [FromBody] UpdatePromotionDto request)
    {
        var result = await _promotionService.UpdatePromotionAsync(id, request);
        if (!result.Success)
        {
            if (result.Code == "NOT_FOUND")
                return NotFound(result);
            if (result.Code == "BAD_REQUEST")
                return BadRequest(result);
            return StatusCode(500, result);
        }
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
