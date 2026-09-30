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
    private readonly IConfiguration _configuration;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger, IConfiguration configuration)
    {
        _paymentService = paymentService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Nhận Webhook từ SePay khi có giao dịch chuyển khoản thành công
    /// </summary>
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SePayWebhook()
    {
        try
        {
            // Đọc raw body
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            // Validate HMAC Signature
            var signature = Request.Headers["SePay-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Missing SePay signature.");
                return Ok(new { success = false, message = "Missing signature" }); 
            }

            var webhookSecret = _configuration["SePay:WebhookSecret"];
            if (!string.IsNullOrEmpty(webhookSecret))
            {
                using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(webhookSecret));
                var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawBody));
                var computedSignature = Convert.ToHexString(hashBytes).ToLower();

                if (computedSignature != signature.ToLower())
                {
                    _logger.LogWarning("Invalid SePay signature.");
                    return StatusCode(403, new { success = false, message = "Invalid signature" });
                }
            }

            var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null)
            {
                return BadRequest(new { success = false, message = "Invalid payload" });
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
