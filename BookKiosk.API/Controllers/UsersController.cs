using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] UserFilterDto? filter)
    {
        var result = await _userService.GetUsersAsync(filter ?? new UserFilterDto());
        return Ok(ApiResponseDto<PaginatedResultDto<UserDto>>.Ok(result, "Lấy danh sách người dùng thành công."));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", $"Không tìm thấy người dùng với ID = {id}."));
        }

        return Ok(ApiResponseDto<UserDto>.Ok(user, "Lấy thông tin người dùng thành công."));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        try
        {
            var result = await _userService.CreateUserAsync(dto);
            return StatusCode(StatusCodes.Status201Created, ApiResponseDto<UserDto>.Ok(result, "Tạo tài khoản người dùng thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        try
        {
            var result = await _userService.UpdateUserAsync(id, dto);
            return Ok(ApiResponseDto<UserDto>.Ok(result, "Cập nhật thông tin người dùng thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDto dto)
    {
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        int.TryParse(currentUserIdClaim, out var currentAdminId);

        try
        {
            var result = await _userService.UpdateUserStatusAsync(id, dto.IsActive, currentAdminId);
            return Ok(ApiResponseDto<UserDto>.Ok(result, "Cập nhật trạng thái người dùng thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _userService.ResetPasswordAsync(id, dto.NewPassword);
            return Ok(ApiResponseDto<object?>.Ok(null, "Đặt lại mật khẩu thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
        }
    }
}
