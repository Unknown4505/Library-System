using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services.Api;

/// <summary>
/// Triển khai toàn bộ API call của Kiosk — tất cả lỗi mạng được bắt ở đây,
/// đảm bảo Kiosk KHÔNG BAO GIỜ bị crash vì network exception.
/// </summary>
public class BookKioskApiClient : IBookKioskApiClient
{
    private readonly HttpClient _httpClient;

    public BookKioskApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Helper Wrapper ────────────────────────────────────────────────────────

    /// <summary>
    /// Bọc mọi HTTP request — bắt lỗi mạng, parse JSON, trả về ApiResponseDto nhất quán.
    /// FE không cần try/catch, chỉ cần kiểm tra .Success
    /// </summary>
    private async Task<ApiResponseDto<T>?> SendAsync<T>(Func<Task<HttpResponseMessage>> requestFunc)
    {
        try
        {
            var response = await requestFunc();

            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<T>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? ApiResponseDto<T>.Error("JSON_PARSE_ERROR", "Không thể đọc dữ liệu từ Server.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<T>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ApiResponseDto<T>.Error("TIMEOUT_ERROR", "Yêu cầu tới Server bị hết giờ (timeout 30s).");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<T>.Error("UNKNOWN_ERROR", $"Lỗi không xác định: {ex.Message}");
        }
    }

    // ── BOOKS ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponseDto<PaginatedResultDto<BookDto>>?> GetBooksAsync(
        int page = 1, int pageSize = 12, string? keyword = null, int? categoryId = null)
    {
        var query = $"api/books?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword))
            query += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (categoryId.HasValue)
            query += $"&categoryId={categoryId.Value}";

        return await SendAsync<PaginatedResultDto<BookDto>>(() => _httpClient.GetAsync(query));
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByBarcodeAsync(string barcode)
    {
        return await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/barcode/{Uri.EscapeDataString(barcode)}"));
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
    {
        return await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/{bookId}"));
    }

    // ── CATEGORIES & AREAS ───────────────────────────────────────────────────

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
    {
        return await SendAsync<List<CategoryDto>>(() => _httpClient.GetAsync("api/categories"));
    }

    public async Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync()
    {
        return await SendAsync<List<AreaDto>>(() => _httpClient.GetAsync("api/areas"));
    }

    // ── MEMBERS ───────────────────────────────────────────────────────────────

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
    {
        return await SendAsync<MemberDto>(() => _httpClient.GetAsync($"api/members/{Uri.EscapeDataString(phoneNumber)}"));
    }

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
    {
        return await SendAsync<MemberDto>(() => _httpClient.PostAsJsonAsync("api/members", request));
    }

    // ── ORDERS & CHECKOUT (Bypass SendAsync) ──────────────────────────────────

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/orders/kiosk/checkout", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<CheckoutResponseDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            return result ?? ApiResponseDto<CheckoutResponseDto>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<CheckoutResponseDto>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<CheckoutResponseDto>.Error("ERROR", ex.Message);
        }
    }

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/orders/kiosk/{orderId}/cancel", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            return result ?? ApiResponseDto<object>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<object>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<object>.Error("ERROR", ex.Message);
        }
    }

    // ── PAYMENT POLLING ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<PaymentStatusDto>?> GetPaymentStatusAsync(int orderId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/orders/kiosk/{orderId}/payment-status");
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<PaymentStatusDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result ?? ApiResponseDto<PaymentStatusDto>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<PaymentStatusDto>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PaymentStatusDto>.Error("ERROR", ex.Message);
        }
    }

    // ── RECEIPT ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy hóa đơn chính thức từ server cho đơn hàng Kiosk đã thanh toán (Task 8.2.1, Issue #29).
    /// Phân loại: Success(ReceiptDto) | NotFound(404) | Conflict(409) | Timeout | Network | ServerError (5xx).
    /// </summary>
    public async Task<ApiResponseDto<ReceiptDto>?> GetReceiptAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/orders/kiosk/{orderId}/receipt", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var errorObj = await response.Content.ReadFromJsonAsync<ApiResponseDto<ReceiptDto>>(
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
                return errorObj ?? ApiResponseDto<ReceiptDto>.Error("NOT_FOUND", "Không tìm thấy hóa đơn cho đơn hàng này.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorObj = await response.Content.ReadFromJsonAsync<ApiResponseDto<ReceiptDto>>(
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
                return errorObj ?? ApiResponseDto<ReceiptDto>.Error("CONFLICT", "Đơn hàng chưa hoàn tất thanh toán hoặc đã bị hủy.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponseDto<ReceiptDto>.Error("SERVER_ERROR", $"Lỗi máy chủ: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<ReceiptDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);

            return result ?? ApiResponseDto<ReceiptDto>.Error("PARSE_ERROR", "Không thể đọc dữ liệu hóa đơn từ máy chủ.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<ReceiptDto>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ: {ex.Message}");
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResponseDto<ReceiptDto>.Error("CANCELLED", "Yêu cầu lấy hóa đơn đã bị hủy.");
        }
        catch (TaskCanceledException)
        {
            return ApiResponseDto<ReceiptDto>.Error("TIMEOUT_ERROR", "Yêu cầu tới máy chủ bị hết giờ (timeout 30s).");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ReceiptDto>.Error("UNKNOWN_ERROR", $"Lỗi không xác định: {ex.Message}");
        }
    }

    // ── KIOSK HEARTBEAT ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<object>?> SendHeartbeatAsync(int kioskId, int status = 1, string? errorCode = null, string? errorMessage = null)
    {
        return await SendAsync<object>(() => _httpClient.PostAsJsonAsync("api/kiosk/heartbeat", new { kioskId, status, errorCode, errorMessage }));
    }

    public Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId)
    {
        return int.TryParse(kioskId, out var databaseId)
            ? SendHeartbeatAsync(databaseId)
            : Task.FromResult<ApiResponseDto<object>?>(
                ApiResponseDto<object>.Error("INVALID_KIOSK_ID", "Kiosk ID must be the numeric ID assigned by the database."));
    }

    public async Task<ApiResponseDto<object>?> SimulatePaymentWebhookAsync(string orderCode, decimal amount)
    {
        var payload = new
        {
            referenceCode = "DEV-" + DateTime.Now.Ticks.ToString(),
            amountIn = amount,
            transactionContent = orderCode
        };

        return await SendAsync<object>(() => _httpClient.PostAsJsonAsync("api/payments/sepay-webhook", payload));
    }

    public async Task<ApiResponseDto<object>?> DevSimulatePaymentSuccessAsync(string orderCode)
    {
        return await SendAsync<object>(() => _httpClient.PostAsync($"api/payments/dev-simulate-success/{Uri.EscapeDataString(orderCode)}", null));
    }
}
