using BookKiosk.Application.DTOs.Auth;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponseDto<LoginResponseDto>.Ok(result, "Đăng nhập thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponseDto<object>.Error("UNAUTHORIZED", ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, ApiResponseDto<object>.Error("SERVER_ERROR", "Đã xảy ra lỗi trong quá trình đăng nhập."));
        }
    }
}
