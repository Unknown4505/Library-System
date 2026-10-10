using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Kiosk gửi yêu cầu thanh toán (Checkout)
    /// </summary>
    [HttpPost("kiosk/checkout")]
    public async Task<IActionResult> CheckoutKiosk([FromBody] CheckoutRequestDto request)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Giỏ hàng rỗng."));

        try
        {
            var response = await _orderService.CheckoutKioskAsync(request);
            return Ok(ApiResponseDto<CheckoutResponseDto>.Ok(response));
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpPost("kiosk/{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(int orderId)
    {
        try
        {
            await _orderService.CancelOrderAsync(orderId);
            return Ok(ApiResponseDto<object>.Ok(null, "Đã hủy đơn hàng và hoàn trả số lượng sách về kho."));
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponseDto<object>.Error("CONFLICT", ex.Message));
        }
    }

    [HttpGet("kiosk/{orderId}/payment-status")]
    public async Task<IActionResult> GetPaymentStatus(int orderId)
    {
        try
        {
            var statusDto = await _orderService.GetPaymentStatusAsync(orderId);
            return Ok(ApiResponseDto<PaymentStatusDto>.Ok(statusDto));
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
    }
}
