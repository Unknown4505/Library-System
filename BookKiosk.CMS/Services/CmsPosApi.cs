using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    public class CmsPosApi : IPosApi
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CmsPosApi> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CmsPosApi(HttpClient httpClient, ILogger<CmsPosApi> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        #region Catalog & Books

        public async Task<ApiResult<PosBookDto>> GetBookByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return ApiResult<PosBookDto>.Fail("Mã vạch không được để trống.", statusCode: 400);

            var endpoint = $"/api/books/barcode/{Uri.EscapeDataString(barcode.Trim())}";
            return await ExecuteRequestAsync<PosBookDto>(HttpMethod.Get, endpoint);
        }

        public async Task<ApiResult<PaginatedResult<PosBookDto>>> SearchBooksAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 12)
        {
            var queryParams = new List<string>
            {
                $"page={Math.Max(1, page)}",
                $"pageSize={Math.Max(1, pageSize)}"
            };

            if (!string.IsNullOrWhiteSpace(keyword))
                queryParams.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");

            if (categoryId.HasValue && categoryId.Value > 0)
                queryParams.Add($"categoryId={categoryId.Value}");

            var endpoint = $"/api/books?{string.Join("&", queryParams)}";
            return await ExecuteRequestAsync<PaginatedResult<PosBookDto>>(HttpMethod.Get, endpoint);
        }

        public async Task<ApiResult<IEnumerable<PosCategoryDto>>> GetCategoriesAsync()
        {
            return await ExecuteRequestAsync<IEnumerable<PosCategoryDto>>(HttpMethod.Get, "/api/categories");
        }

        #endregion

        #region Promotions

        public async Task<ApiResult<IEnumerable<PosPromotionDto>>> GetActivePromotionsAsync()
        {
            // Backend chỉ có GET /api/promotions (có [Authorize])
            var result = await ExecuteRequestAsync<IEnumerable<PosPromotionDto>>(HttpMethod.Get, "/api/promotions");
            if (!result.Success || result.Data == null)
                return result;

            // Lọc client-side: Đang active và nằm trong khung thời gian hiệu lực
            var now = DateTime.UtcNow;
            var activeList = result.Data
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .ToList();

            return ApiResult<IEnumerable<PosPromotionDto>>.Ok(activeList);
        }

        #endregion

        #region Members

        public async Task<ApiResult<PosMemberDto>> GetMemberByPhoneAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return ApiResult<PosMemberDto>.Fail("Số điện thoại không được để trống.", statusCode: 400);

            var endpoint = $"/api/members/{Uri.EscapeDataString(phoneNumber.Trim())}";
            return await ExecuteRequestAsync<PosMemberDto>(HttpMethod.Get, endpoint);
        }

        public async Task<ApiResult<PosMemberDto>> CreateMemberAsync(PosCreateMemberDto dto)
        {
            return await ExecuteRequestAsync<PosMemberDto>(HttpMethod.Post, "/api/members", dto);
        }

        #endregion

        #region Counter Orders (Checkout, Confirm, Cancel, Status)

        public async Task<ApiResult<CounterCheckoutResponseDto>> CheckoutCounterAsync(CounterCheckoutRequestDto dto)
        {
            return await ExecuteRequestAsync<CounterCheckoutResponseDto>(HttpMethod.Post, "/api/orders/counter/checkout", dto);
        }

        public async Task<ApiResult<PosReceiptDto>> ConfirmPaymentAsync(int orderId)
        {
            var endpoint = $"/api/orders/counter/{orderId}/confirm-payment";
            return await ExecuteRequestAsync<PosReceiptDto>(HttpMethod.Post, endpoint);
        }

        public async Task<ApiResult<bool>> CancelOrderAsync(int orderId)
        {
            var endpoint = $"/api/orders/counter/{orderId}/cancel";
            return await ExecuteRequestAsync<bool>(HttpMethod.Post, endpoint);
        }

        public async Task<ApiResult<OrderPaymentStatusDto>> GetOrderStatusAsync(int orderId)
        {
            var endpoint = $"/api/orders/{orderId}";
            return await ExecuteRequestAsync<OrderPaymentStatusDto>(HttpMethod.Get, endpoint);
        }

        #endregion

        #region Core Request Execution with EndpointUnavailable Detection

        private async Task<ApiResult<T>> ExecuteRequestAsync<T>(HttpMethod method, string endpoint, object? body = null)
        {
            try
            {
                using var request = new HttpRequestMessage(method, endpoint);
                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: _jsonOptions);
                }
                else if (method == HttpMethod.Post || method == HttpMethod.Put)
                {
                    request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                // Lấy server time từ Header Date nếu có
                DateTime? serverDate = null;
                if (response.Headers.Date.HasValue)
                {
                    serverDate = response.Headers.Date.Value.UtcDateTime;
                }

                // 1. Phân biệt 404 Kỹ thuật vs 404 Nghiệp vụ (Quy tắc Bước A)
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    bool isBusinessNotFound = false;
                    string businessMessage = "Không tìm thấy dữ liệu.";

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try
                        {
                            var errorDoc = JsonDocument.Parse(content);
                            if (errorDoc.RootElement.TryGetProperty("code", out var codeProp))
                            {
                                var codeVal = codeProp.GetString();
                                if (!string.IsNullOrEmpty(codeVal) && codeVal.Equals("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                            {
                                    isBusinessNotFound = true;
                                    if (errorDoc.RootElement.TryGetProperty("message", out var msgProp))
                                    {
                                        businessMessage = msgProp.GetString() ?? businessMessage;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Không parse được JSON wrapper -> 404 Kỹ thuật (Endpoint chưa có)
                        }
                    }

                    if (!isBusinessNotFound)
                    {
                        _logger.LogWarning("Endpoint chưa sẵn sàng trên Backend: {Method} {Endpoint} (Status {StatusCode})", method, endpoint, (int)response.StatusCode);
                        return ApiResult<T>.EndpointUnavailable(endpoint, method.Method, (int)response.StatusCode);
                    }

                    return ApiResult<T>.Fail(businessMessage, statusCode: 404);
                }

                // 2. Thành công 200 - 299
                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return ApiResult<T>.Ok(default!);
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("data", out var dataElement))
                        {
                            var extractedData = JsonSerializer.Deserialize<T>(dataElement.GetRawText(), _jsonOptions);
                            
                            // Nếu là CounterCheckoutResponseDto, inject ServerTime và tính RemainingSeconds
                            if (extractedData is CounterCheckoutResponseDto checkoutResp)
                            {
                                var refTime = serverDate ?? DateTime.UtcNow;
                                checkoutResp.ServerTime = refTime;
                                if (checkoutResp.ExpiresAt.HasValue)
                                {
                                    checkoutResp.RemainingSeconds = Math.Max(0, (int)(checkoutResp.ExpiresAt.Value - refTime).TotalSeconds);
                                }
                            }
                            else if (extractedData is OrderPaymentStatusDto statusResp)
                            {
                                statusResp.ServerTime = serverDate ?? DateTime.UtcNow;
                            }

                            return ApiResult<T>.Ok(extractedData!);
                        }

                        // Nếu Backend trả trực tiếp object T
                        var directData = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                        if (directData is CounterCheckoutResponseDto directCheckout)
                        {
                            var refTime = serverDate ?? DateTime.UtcNow;
                            directCheckout.ServerTime = refTime;
                            if (directCheckout.ExpiresAt.HasValue)
                            {
                                directCheckout.RemainingSeconds = Math.Max(0, (int)(directCheckout.ExpiresAt.Value - refTime).TotalSeconds);
                            }
                        }
                        else if (directData is OrderPaymentStatusDto directStatus)
                        {
                            directStatus.ServerTime = serverDate ?? DateTime.UtcNow;
                        }
                        return ApiResult<T>.Ok(directData!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi phân tích JSON response từ {Endpoint}", endpoint);
                        return ApiResult<T>.Fail("Dữ liệu trả về từ máy chủ không đúng định dạng.", statusCode: 500);
                    }
                }

                // 3. Xử lý các mã lỗi nghiệp vụ khác (400, 401, 403, 409, 500)
                string errorMessage = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => "Dữ liệu yêu cầu không hợp lệ (400).",
                    HttpStatusCode.Unauthorized => "Phiên làm việc đã hết hạn hoặc chưa đăng nhập (401).",
                    HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này (403).",
                    HttpStatusCode.Conflict => "Xung đột dữ liệu hoặc sách không đủ tồn kho (409).",
                    HttpStatusCode.InternalServerError => "Máy chủ gặp sự cố nội bộ (500).",
                    _ => $"Lỗi máy chủ HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                };

                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        using var errDoc = JsonDocument.Parse(content);
                        if (errDoc.RootElement.TryGetProperty("message", out var msgProp) && !string.IsNullOrWhiteSpace(msgProp.GetString()))
                        {
                            errorMessage = msgProp.GetString()!;
                        }
                    }
                    catch
                    {
                        // Giữ nguyên message mặc định
                    }
                }

                return ApiResult<T>.Fail(errorMessage, statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi {Method} {Endpoint}", method, endpoint);
                return ApiResult<T>.Fail("Không thể kết nối đến máy chủ API. Vui lòng kiểm tra lại mạng.", statusCode: 0);
            }
        }

        #endregion
    }
}
