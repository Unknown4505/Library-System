using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[ApiController]
[Route("api/books")]
[Authorize(Roles = "Admin")]
public sealed class BooksBarcodeController(IBookBarcodeService service) : ControllerBase
{
    [HttpPost("{id:int}/change-barcode")]
    public async Task<IActionResult> ChangeBarcode(int id, [FromBody] ChangeBarcodeRequestDto request)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(actor, out var actorId) || actorId <= 0)
            return Unauthorized(ApiResponseDto<ChangeBarcodeDto>.Error("UNAUTHORIZED", "Token không chứa định danh người dùng hợp lệ."));

        var response = await service.ChangeAsync(id, request, actorId, HttpContext.TraceIdentifier);
        return response.Code switch
        {
            "SUCCESS" => Ok(response),
            "NOT_FOUND" => NotFound(response),
            "CONFLICT" => Conflict(response),
            "UNAUTHORIZED" => Unauthorized(response),
            _ => BadRequest(response)
        };
    }
}
