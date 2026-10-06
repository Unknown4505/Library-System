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
        try
        {
            var response = await _httpClient.GetAsync($"api/books/barcode/{Uri.EscapeDataString(barcode)}");
            // Đọc JSON bất chấp IsSuccessStatusCode để hứng 404 message
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<BookDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result ?? ApiResponseDto<BookDto>.Error("JSON_PARSE_ERROR", "Không thể đọc dữ liệu từ Server.");
        }
        catch (HttpRequestException ex)
        {
            return ApiResponseDto<BookDto>.Error("NETWORK_ERROR", $"Lỗi mạng: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<BookDto>.Error("ERROR", ex.Message);
        }
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
        => await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/{bookId}"));

    // ── CATEGORIES & AREAS (Bypass SendAsync) ────────────────────────────────

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/categories");
            response.EnsureSuccessStatusCode();
            var rawData = await response.Content.ReadFromJsonAsync<List<CategoryDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return rawData != null ? ApiResponseDto<List<CategoryDto>>.Ok(rawData) 
                                   : ApiResponseDto<List<CategoryDto>>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<CategoryDto>>.Error("ERROR", ex.Message);
        }
    }

    public async Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/areas");
            response.EnsureSuccessStatusCode();
            var rawData = await response.Content.ReadFromJsonAsync<List<AreaDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return rawData != null ? ApiResponseDto<List<AreaDto>>.Ok(rawData) 
                                   : ApiResponseDto<List<AreaDto>>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<AreaDto>>.Error("ERROR", ex.Message);
        }
    }

    // ── MEMBERS ───────────────────────────────────────────────────────────────

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
        => await SendAsync<MemberDto>(() => _httpClient.GetAsync($"api/members/{Uri.EscapeDataString(phoneNumber)}"));

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
        => await SendAsync<MemberDto>(() => _httpClient.PostAsJsonAsync("api/members", request));

    // ── ORDERS & CHECKOUT (Bypass SendAsync) ──────────────────────────────────

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/orders/kiosk/checkout", request);
            response.EnsureSuccessStatusCode();
            var rawData = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
            return rawData != null ? ApiResponseDto<CheckoutResponseDto>.Ok(rawData) 
                                   : ApiResponseDto<CheckoutResponseDto>.Error("PARSE_ERROR", "Lỗi parse JSON");
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
            response.EnsureSuccessStatusCode();
            return ApiResponseDto<object>.Ok(null, "Hủy đơn hàng thành công");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<object>.Error("ERROR", ex.Message);
        }
    }

    // ── PAYMENT POLLING (Custom Raw Parsing) ──────────────────────────────────

    private class RawPaymentStatusResponse 
    { 
        [JsonPropertyName("status")] 
        public string Status { get; set; } = string.Empty; 
    }

    public async Task<string?> GetPaymentStatusAsync(int orderId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/orders/kiosk/{orderId}/payment-status");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RawPaymentStatusResponse>();
            return result?.Status;
        }
        catch
        {
            return null; // Bỏ qua lỗi mạng khi polling để tick sau chạy tiếp
        }
    }

    // ── KIOSK HEARTBEAT ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId, int status, string errorCode = "", string errorMessage = "")
    {
        return await SendAsync<object>(() => _httpClient.PostAsJsonAsync("api/kiosk/heartbeat", new 
        { 
            kioskId = kioskId,
            status = status,
            errorCode = errorCode,
            errorMessage = errorMessage
        }));
    }
}
