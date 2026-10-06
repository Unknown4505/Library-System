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
            return BadRequest("Giỏ hàng rỗng.");

        try
        {
            var response = await _orderService.CheckoutKioskAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            // Trả về 409 Conflict nếu hết hàng hoặc lỗi Logic
            return Conflict(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Kiosk gửi yêu cầu hủy đơn hàng (Khách bấm X)
    /// </summary>
    [HttpPost("kiosk/{orderId}/cancel")]
    public async Task<IActionResult> CancelOrder(int orderId)
    {
        try
        {
            await _orderService.CancelOrderAsync(orderId);
            return Ok(new { Message = "Đã hủy đơn hàng và hoàn trả số lượng sách về kho." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Kiosk polling trạng thái thanh toán
    /// </summary>
    [HttpGet("kiosk/{orderId}/payment-status")]
    public async Task<IActionResult> GetPaymentStatus(int orderId)
    {
        try
        {
            var status = await _orderService.GetPaymentStatusAsync(orderId);
            return Ok(new { status });
        }
        catch (Exception ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }
}
