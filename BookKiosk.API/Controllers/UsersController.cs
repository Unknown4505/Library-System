using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        catch (DbUpdateException)
        {
            // QA-USER-01: Map vi phạm unique constraint đồng thời sang 409 CONFLICT an toàn
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Tên đăng nhập đã tồn tại trong hệ thống."));
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
        catch (DbUpdateConcurrencyException)
        {
            // P2 Hardening: Map concurrency sang thông điệp nghiệp vụ an toàn
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Hệ thống bắt buộc phải có ít nhất một tài khoản Quản trị viên (Admin) đang hoạt động."));
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Yêu cầu không thể hoàn tất do xung đột dữ liệu đồng thời. Vui lòng thử lại sau."));
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusDto dto)
    {
        // QA-USER-04: Validate danh tính actor nghiêm ngặt, từ chối token không có userId hợp lệ
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst("UserId")?.Value
            ?? User.FindFirst("userId")?.Value;

        if (string.IsNullOrWhiteSpace(currentUserIdClaim) || !int.TryParse(currentUserIdClaim, out var currentAdminId) || currentAdminId <= 0)
        {
            return Unauthorized(ApiResponseDto<object>.Error("UNAUTHORIZED", "Token không chứa định danh người dùng hợp lệ."));
        }

        // QA-USER-03: Validate bắt buộc sự hiện diện của trường isActive
        if (dto == null || !dto.IsActive.HasValue)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Trường isActive là bắt buộc."));
        }

        try
        {
            var result = await _userService.UpdateUserStatusAsync(id, dto.IsActive.Value, currentAdminId);
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
        catch (DbUpdateConcurrencyException)
        {
            // P2 Hardening: Map concurrency sang thông điệp nghiệp vụ an toàn
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Hệ thống bắt buộc phải có ít nhất một tài khoản Quản trị viên (Admin) đang hoạt động."));
        }
        catch (DbUpdateException)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Yêu cầu không thể hoàn tất do xung đột dữ liệu đồng thời. Vui lòng thử lại sau."));
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
        catch (DbUpdateException)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Yêu cầu không thể hoàn tất do xung đột dữ liệu đồng thời. Vui lòng thử lại sau."));
        }
    }
}
