using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Reports;

namespace BookKiosk.CMS.Services
{
    public class CmsReportApi : IReportApi
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CmsReportApi> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CmsReportApi(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CmsReportApi> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<ApiResult<RevenueReportResponseDto>> GetRevenueByMonthAsync(int year)
        {
            var endpoint = $"/api/reports/revenue?year={year}";
            return await ExecuteGetAsync<RevenueReportResponseDto>(endpoint);
        }

        public async Task<ApiResult<IEnumerable<TopSellingBookDto>>> GetTopSellingBooksAsync(int top = 10)
        {
            var endpoint = $"/api/reports/top-books?top={Math.Max(1, top)}";
            var result = await ExecuteGetAsync<List<TopSellingBookDto>>(endpoint);
            if (result.Success && result.Data != null)
            {
                return ApiResult<IEnumerable<TopSellingBookDto>>.Ok(result.Data);
            }
            if (result.IsEndpointUnavailable)
            {
                return ApiResult<IEnumerable<TopSellingBookDto>>.EndpointUnavailable(endpoint, "GET", result.StatusCode);
            }
            return ApiResult<IEnumerable<TopSellingBookDto>>.Fail(result.Message ?? "Không thể tải danh sách top sách bán chạy.", result.StatusCode);
        }

        private async Task<ApiResult<T>> ExecuteGetAsync<T>(string endpoint)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                // 1. Phân biệt 404 Kỹ thuật vs 404 Nghiệp vụ
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    bool isBusinessNotFound = false;
                    string businessMessage = "Không tìm thấy dữ liệu báo cáo.";

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
                            // Không parse được JSON wrapper -> 404 Kỹ thuật
                        }
                    }

                    if (!isBusinessNotFound)
                    {
                        _logger.LogWarning("Endpoint báo cáo chưa sẵn sàng trên Backend: GET {Endpoint} (Status {StatusCode})", endpoint, (int)response.StatusCode);
                        return ApiResult<T>.EndpointUnavailable(endpoint, "GET", (int)response.StatusCode);
                    }

                    return ApiResult<T>.Fail(businessMessage, statusCode: 404);
                }

                // 2. Xử lý 401 / 403 Forbidden
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return ApiResult<T>.Fail("Bạn không có quyền truy cập dữ liệu báo cáo (Yêu cầu quyền Admin).", statusCode: 403);
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return ApiResult<T>.Fail("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", statusCode: 401);
                }

                // 3. Xử lý thành công
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

                        // Fallback: deserialize toàn bộ nội dung
                        var directData = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                        return ApiResult<T>.Ok(directData!);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi phân tích cú pháp JSON phản hồi từ {Endpoint}", endpoint);
                        return ApiResult<T>.Fail("Lỗi định dạng dữ liệu phản hồi từ máy chủ.", statusCode: 502);
                    }
                }

                // 4. Các mã lỗi khác từ Backend
                string errorMessage = $"Yêu cầu báo cáo thất bại với mã lỗi HTTP {(int)response.StatusCode}.";
                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        using var errDoc = JsonDocument.Parse(content);
                        if (errDoc.RootElement.TryGetProperty("message", out var m))
                        {
                            errorMessage = m.GetString() ?? errorMessage;
                        }
                    }
                    catch { }
                }

                return ApiResult<T>.Fail(errorMessage, statusCode: (int)response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Lỗi mạng khi kết nối đến API Báo cáo: GET {Endpoint}", endpoint);
                return ApiResult<T>.Fail("Không thể kết nối đến máy chủ Backend. Vui lòng kiểm tra kết nối mạng.", statusCode: 503);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ngoại lệ không xác định khi gọi API Báo cáo: GET {Endpoint}", endpoint);
                return ApiResult<T>.Fail("Đã xảy ra lỗi không xác định khi tải dữ liệu báo cáo.", statusCode: 500);
            }
        }
    }
}
