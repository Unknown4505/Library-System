using System.Net.Http;
using System.Net.Http.Json;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services.Api;

public class BookKioskApiClient : IBookKioskApiClient
{
    private readonly HttpClient _httpClient;

    public BookKioskApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Hàm Wrapper chung xử lý mọi request và hứng lỗi Network (Tối ưu C# nhất)
    private async Task<ApiResponseDto<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> requestFunc)
    {
        try
        {
            var response = await requestFunc();
            
            // Dù HTTP Status Code là 400 (hết hàng) hay 200, Backend của chúng ta đều trả về ApiResponseDto<T>
            var result = await response.Content.ReadFromJsonAsync<ApiResponseDto<T>>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result != null) return result;
            
            return ApiResponseDto<T>.Error("JSON_PARSE_ERROR", "Không thể đọc dữ liệu từ Server.");
        }
        catch (HttpRequestException ex)
        {
            // Bắt lỗi mất mạng, server down, từ chối kết nối (Ngăn Crash Kiosk UI)
            return ApiResponseDto<T>.Error("NETWORK_ERROR", $"Lỗi kết nối tới máy chủ API: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Bắt các lỗi không xác định khác
            return ApiResponseDto<T>.Error("UNKNOWN_ERROR", $"Lỗi không xác định ở Kiosk: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        return await SendAsync<CheckoutResponseDto>(() => _httpClient.PostAsJsonAsync("api/orders/kiosk/checkout", request));
    }

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
    {
        return await SendAsync<object>(() => _httpClient.PostAsync($"api/orders/kiosk/{orderId}/cancel", null));
    }
}
