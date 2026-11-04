using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}

