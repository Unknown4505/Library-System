using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    public class CmsOrderApi : IOrderApi
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CmsOrderApi> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CmsOrderApi(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CmsOrderApi> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<ApiResult<PaginatedResult<OrderListItemDto>>> GetOrdersAsync(OrderFilterDto filter)
        {
            var queryParams = new List<string>
            {
                $"page={Math.Max(1, filter.Page)}",
                $"pageSize={Math.Clamp(filter.PageSize, 1, 100)}"
            };

            // Tìm kiếm chuẩn hóa: Chỉ tìm theo mã đơn hàng ORD-yyyyMMdd-XXXX
            if (!string.IsNullOrWhiteSpace(filter.OrderCode))
            {
                queryParams.Add($"keyword={Uri.EscapeDataString(filter.OrderCode.Trim())}");
            }

            if (filter.Status.HasValue)
            {
                queryParams.Add($"status={(int)filter.Status.Value}");
            }

            if (filter.Channel.HasValue)
            {
                queryParams.Add($"channel={(int)filter.Channel.Value}");
            }

            if (filter.PaymentMethod.HasValue)
            {
                queryParams.Add($"paymentMethod={(int)filter.PaymentMethod.Value}");
            }

            if (filter.FromDate.HasValue)
            {
                queryParams.Add($"fromDate={filter.FromDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
            }

            if (filter.ToDate.HasValue)
            {
                queryParams.Add($"toDate={filter.ToDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
            }

            if (filter.NeedsSupportOnly.HasValue && filter.NeedsSupportOnly.Value)
            {
                queryParams.Add("needsSupport=true");
            }

            var endpoint = $"/api/orders?{string.Join("&", queryParams)}";
            return await ExecuteRequestAsync<PaginatedResult<OrderListItemDto>>(HttpMethod.Get, endpoint);
        }

        public async Task<ApiResult<OrderDetailDto>> GetOrderByIdAsync(int orderId)
        {
            if (orderId <= 0)
                return ApiResult<OrderDetailDto>.Fail("Mã đơn hàng không hợp lệ.", 400);

            var endpoint = $"/api/orders/{orderId}";
            return await ExecuteRequestAsync<OrderDetailDto>(HttpMethod.Get, endpoint);
        }

        public async Task<ApiResult<PosReceiptDto>> GetOrderReceiptAsync(int orderId)
        {
            var orderResult = await GetOrderByIdAsync(orderId);
            if (!orderResult.Success || orderResult.Data == null)
            {
                if (orderResult.IsEndpointUnavailable)
                {
                    return ApiResult<PosReceiptDto>.EndpointUnavailable($"/api/orders/{orderId}", "GET", orderResult.StatusCode);
                }
                return ApiResult<PosReceiptDto>.Fail(orderResult.Message ?? "Không tìm thấy thông tin đơn hàng.", orderResult.StatusCode);
            }

            var order = orderResult.Data;
            if (order.OrderStatus != OrderStatus.Paid)
            {
                return ApiResult<PosReceiptDto>.Fail(
                    $"Chỉ có thể in lại hóa đơn cho đơn hàng đã thanh toán thành công (Paid). Trạng thái hiện tại: {order.OrderStatus}",
                    409);
            }

            var showMemberName = _configuration.GetValue<bool>("Pos:ShowMemberNameOnReceipt", false);
            var memberDisplayName = "Thành viên";
            if (showMemberName && !string.IsNullOrWhiteSpace(order.MemberName))
            {
                memberDisplayName = order.MemberName;
            }

            var receipt = new PosReceiptDto
            {
                OrderCode = order.OrderCode,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt ?? order.CreatedAt,
                CashierName = order.CashierName ?? "Thu ngân",
                MemberDisplayName = memberDisplayName,
                PointsEarned = order.PointsEarned,
                SubTotal = order.SubTotal,
                PromotionId = order.PromotionId,
                PromotionName = order.PromotionName,
                PromotionType = order.PromotionType,
                DiscountAmount = order.DiscountAmount,
                PointsUsed = order.PointsUsed,
                PointsUsedAmount = order.PointsUsedAmount,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod.ToString(),
                StoreName = _configuration.GetValue<string>("Pos:StoreInfo:Name") ?? "NHÀ SÁCH BOOKKIOSK",
                StoreAddress = _configuration.GetValue<string>("Pos:StoreInfo:Address") ?? "Điểm bán lẻ Kiosk & Quầy thu ngân",
                StoreHotline = _configuration.GetValue<string>("Pos:StoreInfo:Hotline") ?? "1900 6868",
                StoreWebsite = _configuration.GetValue<string>("Pos:StoreInfo:Website") ?? "bookkiosk.vn",
                Items = order.Items.Select(i => new PosReceiptItemDto
                {
                    BookId = i.BookId,
                    Title = i.BookTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPriceAtTime,
                    DiscountPerUnit = i.DiscountPerUnit,
                    LineTotal = i.LineTotal,
                    LineDiscountAmount = i.LineDiscountAmount,
                    NetLineTotal = i.NetLineTotal
                }).ToList()
            };

            return ApiResult<PosReceiptDto>.Ok(receipt);
        }

        public async Task<ApiResult<int>> GetNeedsSupportCountAsync()
        {
            try
            {
                var result = await GetOrdersAsync(new OrderFilterDto
                {
                    NeedsSupportOnly = true,
                    Page = 1,
                    PageSize = 1
                });

                if (result.Success && result.Data != null)
                {
                    return ApiResult<int>.Ok(result.Data.TotalCount);
                }

                // Nếu Backend chưa có endpoint /api/orders, trả về 0 để không lỗi chuông thông báo
                return ApiResult<int>.Ok(0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi lấy số lượng đơn Cần hỗ trợ cho chuông thông báo");
                return ApiResult<int>.Ok(0);
            }
        }

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

                // 1. Phân biệt 404 Kỹ thuật (chưa triển khai route) vs 404 Nghiệp vụ
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    bool isBusinessNotFound = false;
                    string businessMessage = "Không tìm thấy dữ liệu đơn hàng.";

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try
                        {
                            using var errorDoc = JsonDocument.Parse(content);
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
                            return ApiResult<T>.Ok(extractedData!);
                        }

                        var directData = JsonSerializer.Deserialize<T>(content, _jsonOptions);
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
                    HttpStatusCode.Conflict => "Xung đột dữ liệu đơn hàng (409).",
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
                        // Giữ nguyên errorMessage mặc định
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
