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
    private readonly IKioskAdvertisingService _advertisingService;

    public KioskController(IKioskService kioskService, IKioskAdvertisingService advertisingService)
    {
        _kioskService = kioskService;
        _advertisingService = advertisingService;
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

    /// <summary>
    /// Kiosk đọc dữ liệu quảng cáo (khuyến mãi, sách mới, sách bán chạy)
    /// </summary>
    [HttpGet("advertising")]
    public async Task<IActionResult> GetAdvertising()
    {
        try
        {
            var result = await _advertisingService.GetAdvertisingAsync();
            return result.Status switch
            {
                AdvertisingStatus.ServiceUnavailable => StatusCode(503, ApiResponseDto<KioskAdvertisingDto>.ServiceUnavailable(result.Message)),
                AdvertisingStatus.PartialSuccess => Ok(ApiResponseDto<KioskAdvertisingDto>.PartialSuccess(result.Data, result.Message)),
                _ => Ok(ApiResponseDto<KioskAdvertisingDto>.Ok(result.Data, result.Message))
            };
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponseDto<object>.Error("INTERNAL_ERROR", "Hệ thống gặp sự cố. Vui lòng thử lại sau."));
        }
    }
}
