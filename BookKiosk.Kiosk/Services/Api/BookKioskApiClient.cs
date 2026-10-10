using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Kiosk.Models;

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

        var result = await SendAsync<PaginatedResultDto<BookDto>>(() => _httpClient.GetAsync(query));
        if (result != null && !result.Success && result.Code == "NETWORK_ERROR")
        {
            var mockQuery = MockDataStore.Books.AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
                mockQuery = mockQuery.Where(b => b.Title.ToLower().Contains(keyword.ToLower()) || b.Author.ToLower().Contains(keyword.ToLower()));
            if (categoryId.HasValue && categoryId.Value > 0)
                mockQuery = mockQuery.Where(b => b.CategoryId == categoryId.Value);

            var totalCount = mockQuery.Count();
            var items = mockQuery.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(b => new BookDto { BookId = b.BookId, Barcode = b.Barcode, Title = b.Title, Author = b.Author, SellingPrice = b.SellingPrice, ImageUrl = b.ImageUrl, CategoryId = b.CategoryId, AreaName = b.AreaName ?? "", AvailableStock = b.AvailableStock }).ToList();

            return ApiResponseDto<PaginatedResultDto<BookDto>>.Ok(new PaginatedResultDto<BookDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
        }
        return result;
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByBarcodeAsync(string barcode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/books/barcode/{Uri.EscapeDataString(barcode)}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<BookDto>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result ?? ApiResponseDto<BookDto>.Error("JSON_PARSE_ERROR", "Không thể đọc dữ liệu từ Server.");
        }
        catch (HttpRequestException ex)
        {
            var mock = MockDataStore.Books.FirstOrDefault(b => b.Barcode == barcode);
            if (mock != null) return ApiResponseDto<BookDto>.Ok(new BookDto { BookId = mock.BookId, Barcode = mock.Barcode, Title = mock.Title, Author = mock.Author, SellingPrice = mock.SellingPrice, ImageUrl = mock.ImageUrl, CategoryId = mock.CategoryId, AreaName = mock.AreaName ?? "", AvailableStock = mock.AvailableStock });
            return ApiResponseDto<BookDto>.Error("NETWORK_ERROR", $"Lỗi mạng: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<BookDto>.Error("ERROR", ex.Message);
        }
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
    {
        var result = await SendAsync<BookDto>(() => _httpClient.GetAsync($"api/books/{bookId}"));
        if (result != null && !result.Success && result.Code == "NETWORK_ERROR")
        {
            var mock = MockDataStore.Books.FirstOrDefault(b => b.BookId == bookId);
            if (mock != null) return ApiResponseDto<BookDto>.Ok(new BookDto { BookId = mock.BookId, Barcode = mock.Barcode, Title = mock.Title, Author = mock.Author, SellingPrice = mock.SellingPrice, ImageUrl = mock.ImageUrl, CategoryId = mock.CategoryId, AreaName = mock.AreaName ?? "", AvailableStock = mock.AvailableStock });
        }
        return result;
    }

    // ── CATEGORIES & AREAS (Bypass SendAsync) ────────────────────────────────

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/categories");
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CategoryDto>>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result ?? ApiResponseDto<List<CategoryDto>>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (HttpRequestException ex)
        {
            var mocks = MockDataStore.Categories.Select(c => new CategoryDto { CategoryId = c.Id, Name = c.Name }).ToList();
            if (mocks.Any()) return ApiResponseDto<List<CategoryDto>>.Ok(mocks);
            return ApiResponseDto<List<CategoryDto>>.Error("NETWORK_ERROR", $"Lỗi kết nối: {ex.Message}");
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
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AreaDto>>>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result ?? ApiResponseDto<List<AreaDto>>.Error("PARSE_ERROR", "Lỗi parse JSON");
        }
        catch (HttpRequestException)
        {
            return ApiResponseDto<List<AreaDto>>.Ok(new List<AreaDto>()); // Mock trả về rỗng
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<AreaDto>>.Error("ERROR", ex.Message);
        }
    }

    // ── MEMBERS ───────────────────────────────────────────────────────────────

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
    {
        var result = await SendAsync<MemberDto>(() => _httpClient.GetAsync($"api/members/{Uri.EscapeDataString(phoneNumber)}"));
        if (result != null && !result.Success && result.Code == "NETWORK_ERROR")
        {
            var mock = MockDataStore.Members.FirstOrDefault(m => m.PhoneNumber == phoneNumber);
            if (mock != null) return ApiResponseDto<MemberDto>.Ok(mock);
            return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên (Mock)");
        }
        return result;
    }

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
    {
        var result = await SendAsync<MemberDto>(() => _httpClient.PostAsJsonAsync("api/members", request));
        if (result != null && !result.Success && result.Code == "NETWORK_ERROR")
        {
            var newMock = new MemberDto { MemberId = MockDataStore.Members.Max(m => m.MemberId) + 1, PhoneNumber = request.PhoneNumber, FullName = request.FullName, Points = 0 };
            MockDataStore.Members.Add(newMock);
            return ApiResponseDto<MemberDto>.Ok(newMock);
        }
        return result;
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

    // ── KIOSK HEARTBEAT ───────────────────────────────────────────────────────

    public async Task<ApiResponseDto<object>?> SendHeartbeatAsync(int kioskId, int status = 1, string? errorCode = null, string? errorMessage = null)
    {
        var result = await SendAsync<object>(() => _httpClient.PostAsJsonAsync("api/kiosk/heartbeat", new { kioskId, status, errorCode, errorMessage }));
        if (result != null && !result.Success && result.Code == "NETWORK_ERROR")
            return ApiResponseDto<object>.Ok(new object());
        return result;
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
}
