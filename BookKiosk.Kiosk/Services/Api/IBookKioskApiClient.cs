using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services.Api;

public interface IBookKioskApiClient
{
    Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request);
    Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId);
}
