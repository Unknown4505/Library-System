using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Enums;
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
    private readonly IWebHostEnvironment _environment;

    public PaymentsController(
        IPaymentService paymentService, 
        ILogger<PaymentsController> logger, 
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _paymentService = paymentService;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
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

            var signature = Request.Headers["X-SePay-Signature"].FirstOrDefault() 
                            ?? Request.Headers["SePay-Signature"].FirstOrDefault();
            var timestampStr = Request.Headers["X-SePay-Timestamp"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Missing SePay signature.");
                return Unauthorized(new { success = false, message = "Missing signature" }); 
            }

            var webhookSecret = _configuration["SePay:WebhookSecret"];
            if (string.IsNullOrEmpty(webhookSecret))
            {
                _logger.LogError("SePay WebhookSecret is not configured. Failing closed.");
                return StatusCode(500, new { success = false, message = "Webhook secret is not configured" });
            }

            // Timestamp anti-replay validation (dung sai ±300 giây = 5 phút theo tài liệu SePay)
            if (!string.IsNullOrEmpty(timestampStr))
            {
                if (!long.TryParse(timestampStr, out var timestamp) || 
                    Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > 300)
                {
                    _logger.LogWarning("SePay webhook timestamp is expired or invalid: {Timestamp}", timestampStr);
                    return Unauthorized(new { success = false, message = "Request expired" });
                }
            }
            else if (!_environment.IsDevelopment())
            {
                _logger.LogWarning("Missing X-SePay-Timestamp header in Production.");
                return Unauthorized(new { success = false, message = "Missing timestamp" });
            }

            // Chuỗi ký chuẩn SePay: {timestamp}.{raw_body}
            var stringToSign = !string.IsNullOrEmpty(timestampStr) ? $"{timestampStr}.{rawBody}" : rawBody;
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(webhookSecret));
            var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(stringToSign));
            var hexHash = Convert.ToHexString(hashBytes).ToLower();

            // So sánh định dạng sha256={hash} hoặc chuỗi hash thuần
            var cleanSignature = signature.Trim().ToLower();
            if (cleanSignature.StartsWith("sha256="))
            {
                cleanSignature = cleanSignature.Substring("sha256=".Length);
            }

            var sigBytes = System.Text.Encoding.UTF8.GetBytes(cleanSignature);
            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(hexHash);
            if (sigBytes.Length != expectedBytes.Length || 
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(sigBytes, expectedBytes))
            {
                _logger.LogWarning("Invalid SePay signature.");
                return Unauthorized(new { success = false, message = "Invalid signature" });
            }

            var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null)
            {
                return BadRequest(new { success = false, message = "Invalid payload" });
            }

            var expectedAcc = _configuration["SePay:AccountNumber"];
            var expectedSub = _configuration["SePay:SubAccount"];

            var result = await _paymentService.HandleSePayWebhookAsync(payload, expectedAcc, expectedSub);
            if (result)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false, message = "Không tìm thấy Order hợp lệ hoặc giao dịch không hợp lệ." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xử lý SePay Webhook.");
            return StatusCode(500, new { success = false });
        }
    }

    /// <summary>
    /// F3 mô phỏng thanh toán thành công trong môi trường Development (dành riêng cho Kiosk test luồng)
    /// </summary>
    [HttpPost("dev-simulate-success/{orderCode}")]
    public async Task<IActionResult> DevSimulateSuccess([FromRoute] string orderCode)
    {
        // Chỉ hỗ trợ trong Development, Production luôn trả 404
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        // Xác thực X-Api-Key của Kiosk
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var expectedApiKey = _configuration["ApiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
        {
            _logger.LogWarning("X-Api-Key không hợp lệ khi gọi dev-simulate-success.");
            return Unauthorized(new ApiResponseDto<object>
            {
                Success = false,
                Message = "X-Api-Key không hợp lệ hoặc bị thiếu."
            });
        }

        try
        {
            var (success, status, message, order) = await _paymentService.SimulateDevSuccessAsync(orderCode);
            if (status == "NotFound" || order == null)
            {
                return NotFound(new ApiResponseDto<object>
                {
                    Success = false,
                    Message = message ?? "Không tìm thấy đơn hàng."
                });
            }

            if (!success || status != "Paid" || order.OrderStatus != OrderStatus.Paid)
            {
                return StatusCode(StatusCodes.Status409Conflict, new ApiResponseDto<object>
                {
                    Success = false,
                    Message = message ?? "Đơn hàng đã bị hủy, không thể mô phỏng thanh toán."
                });
            }

            return Ok(new ApiResponseDto<PaymentStatusDto>
            {
                Success = true,
                Message = message ?? "Thanh toán mô phỏng thành công.",
                Data = new PaymentStatusDto
                {
                    OrderId = order.OrderId,
                    OrderCode = order.OrderCode,
                    Status = order.OrderStatus.ToString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thực hiện mô phỏng thanh toán F3.");
            return StatusCode(500, new ApiResponseDto<object>
            {
                Success = false,
                Message = "Đã xảy ra lỗi nội bộ khi mô phỏng thanh toán."
            });
        }
    }
}
