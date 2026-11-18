using System.Globalization;
using System.Security.Claims;
using BookKiosk.Application.DTOs.Auth;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Users;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(IAccountService accounts) : ControllerBase
{
    [HttpGet("me")]
    public Task<IActionResult> Me(CancellationToken cancellationToken) => WithActorAsync(async id =>
        Ok(ApiResponseDto<UserDto>.Ok(await accounts.GetProfileAsync(id, cancellationToken))), cancellationToken);

    [HttpPost("change-password")]
    [RequestSizeLimit(8192)]
    public Task<IActionResult> ChangePassword(ChangeOwnPasswordDto request, CancellationToken cancellationToken) => WithActorAsync(async id =>
    {
        await accounts.ChangePasswordAsync(id, request, cancellationToken);
        return Ok(ApiResponseDto<object?>.Ok(null, "Đổi mật khẩu thành công."));
    }, cancellationToken);

    private async Task<IActionResult> WithActorAsync(Func<int, Task<IActionResult>> action, CancellationToken cancellationToken)
    {
        var identifiers = User.Claims.Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub").Select(c => c.Value).Distinct().ToArray();
        if (identifiers.Length != 1 || !int.TryParse(identifiers[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            return Unauthorized(ApiResponseDto<object>.Error("UNAUTHORIZED", "Phiên đăng nhập không hợp lệ."));
        try { return await action(id); }
        catch (UnauthorizedAccessException) { return Unauthorized(ApiResponseDto<object>.Error("UNAUTHORIZED", "Phiên đăng nhập không hợp lệ.")); }
        catch (ArgumentException ex) { return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message)); }
        catch (AccountChangedException) { return Conflict(ApiResponseDto<object>.Error("CONFLICT", "Tài khoản đã thay đổi. Vui lòng đăng nhập lại và thử lại.")); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        // Do not log provider exceptions here: they can contain credential parameters.
        catch (Exception) { return StatusCode(500, ApiResponseDto<object>.Error("INTERNAL_ERROR", "Không thể xử lý tài khoản. Vui lòng thử lại sau.")); }
    }
}
