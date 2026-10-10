using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class KioskController : ControllerBase
{
    private readonly IKioskService _kioskService;

    public KioskController(IKioskService kioskService)
    {
        _kioskService = kioskService;
    }

    /// <summary>
    /// Kiosk gửi heartbeat định kỳ
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequestDto request)
    {
        try
        {
            var success = await _kioskService.HandleHeartbeatAsync(request);
            return Ok(ApiResponseDto<bool>.Ok(success));
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
    }
}
