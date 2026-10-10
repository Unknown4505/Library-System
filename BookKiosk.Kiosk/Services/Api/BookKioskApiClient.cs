using System.Net.Http;
using System.Net.Http.Json;
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
        => await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/barcode/{Uri.EscapeDataString(barcode)}"));

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
        => await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/{bookId}"));

    // ── CATEGORIES & AREAS ────────────────────────────────────────────────────

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
        => await SendAsync<List<CategoryDto>>(() => _httpClient.GetAsync("api/categories"));

    public async Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync()
        => await SendAsync<List<AreaDto>>(() => _httpClient.GetAsync("api/areas"));

    // ── MEMBERS ───────────────────────────────────────────────────────────────

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
        => await SendAsync<MemberDto>(() => _httpClient.GetAsync($"api/members/{Uri.EscapeDataString(phoneNumber)}"));

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
        => await SendAsync<MemberDto>(() => _httpClient.PostAsJsonAsync("api/members", request));

    // ── ORDERS & CHECKOUT ─────────────────────────────────────────────────────

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
        => await SendAsync<CheckoutResponseDto>(() => _httpClient.PostAsJsonAsync("api/orders/kiosk/checkout", request));

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
        => await SendAsync<object>(() => _httpClient.PostAsync($"api/orders/kiosk/{orderId}/cancel", null));

    // ── PAYMENT POLLING ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<PaymentStatusDto>?> GetPaymentStatusAsync(int orderId)
        => await SendAsync<PaymentStatusDto>(() => _httpClient.GetAsync($"api/orders/kiosk/{orderId}/payment-status"));

    // ── KIOSK HEARTBEAT ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<object>?> SendHeartbeatAsync(int kioskId, int status = 1, string? errorCode = null, string? errorMessage = null)
        => await SendAsync<object>(() => _httpClient.PostAsJsonAsync("api/kiosk/heartbeat", new { kioskId, status, errorCode, errorMessage }));

    public Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId)
    {
        return int.TryParse(kioskId, out var databaseId)
            ? SendHeartbeatAsync(databaseId)
            : Task.FromResult<ApiResponseDto<object>?>(
                ApiResponseDto<object>.Error("INVALID_KIOSK_ID", "Kiosk ID must be the numeric ID assigned by the database."));
    }
}
