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

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/orders/kiosk/checkout", request);
        // Ngay cả khi status là 400 (hết hàng), BE vẫn trả về ApiResponseDto, 
        // ReadFromJsonAsync sẽ giúp lấy thẳng object lỗi về FE.
        return await response.Content.ReadFromJsonAsync<ApiResponseDto<CheckoutResponseDto>>();
    }

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
    {
        var response = await _httpClient.PostAsync($"api/orders/kiosk/{orderId}/cancel", null);
        return await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
    }
}
