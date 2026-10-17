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
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
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
            return Ok(ApiResponseDto<object?>.Ok(null, "Đã hủy đơn hàng và hoàn trả số lượng sách về kho."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
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

    /// <summary>
    /// Lấy hóa đơn chính thức cho đơn hàng Kiosk đã thanh toán (Task 5.6)
    /// </summary>
    [HttpGet("kiosk/{orderId}/receipt")]
    public async Task<IActionResult> GetReceipt(int orderId)
    {
        try
        {
            var receiptDto = await _orderService.GetReceiptAsync(orderId);
            return Ok(ApiResponseDto<ReceiptDto>.Ok(receiptDto, "Lấy hóa đơn thành công."));
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

    #region POS Counter (Quầy bán hàng CMS)

    [HttpPost("counter/checkout")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> CheckoutCounter([FromBody] CounterCheckoutRequestDto request)
    {
        try
        {
            int cashierId = GetCurrentUserId();
            var result = await _orderService.CheckoutCounterAsync(request, cashierId);
            return Ok(ApiResponseDto<CounterCheckoutResponseDto>.Ok(result, "Tạo đơn hàng tại quầy thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", ex.Message));
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

    [HttpPost("counter/{orderId}/confirm-payment")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> ConfirmCounterPayment(int orderId)
    {
        try
        {
            int cashierId = GetCurrentUserId();
            var receipt = await _orderService.ConfirmCounterPaymentAsync(orderId, cashierId);
            return Ok(ApiResponseDto<PosReceiptDto>.Ok(receipt, "Xác nhận thanh toán tiền mặt thành công."));
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

    [HttpPost("counter/{orderId}/cancel")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> CancelCounterOrder(int orderId)
    {
        try
        {
            int cashierId = GetCurrentUserId();
            await _orderService.CancelCounterOrderAsync(orderId, cashierId);
            return Ok(ApiResponseDto<bool>.Ok(true, "Hủy đơn hàng tại quầy thành công."));
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

    #endregion

    #region Orders CMS (Tra cứu & Đối soát)

    /// <summary>
    /// Endpoint thống nhất: phục vụ cả Polling trạng thái quầy POS (OrderPaymentStatusDto) và xem Chi tiết đơn hàng CMS (OrderDetailDto)
    /// </summary>
    [HttpGet("{orderId:int}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetOrderById(int orderId)
    {
        try
        {
            var orderDetail = await _orderService.GetOrderDetailAsync(orderId);
            return Ok(ApiResponseDto<OrderDetailDto>.Ok(orderDetail, "Lấy thông tin đơn hàng thành công."));
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", ex.Message));
        }
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetOrders([FromQuery] OrderFilterDto filter)
    {
        var result = await _orderService.GetOrdersAsync(filter);
        return Ok(ApiResponseDto<PaginatedResultDto<OrderListItemDto>>.Ok(result, "Lấy danh sách đơn hàng thành công."));
    }

    #endregion

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");
        if (claim != null && int.TryParse(claim.Value, out int userId))
        {
            return userId;
        }
        return 0;
    }
}

