using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Nhận Webhook từ SePay khi có giao dịch chuyển khoản thành công
    /// </summary>
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SePayWebhook([FromBody] PaymentWebhookPayload payload)
    {
        try
        {
            // Validate HMAC Signature (Mô phỏng ở đây, thực tế cần check Header SePay-Signature)
            var signature = Request.Headers["SePay-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Missing SePay signature.");
                // Return 200 để tránh SePay gọi lại n lần cho 1 request lỗi signature (hoặc 400 tùy cấu hình)
                return Ok(new { success = false, message = "Missing signature" }); 
            }

            var result = await _paymentService.HandleSePayWebhookAsync(payload);
            if (result)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, message = "Không tìm thấy Order hợp lệ." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý SePay Webhook.");
            return StatusCode(500, new { success = false });
        }
    }
}
