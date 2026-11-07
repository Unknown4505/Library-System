using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookKiosk.CMS.Models.Auth;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Promotions;
using BookKiosk.CMS.Models.Members;

namespace BookKiosk.CMS.Services
{
    public class CmsApiClient : ICmsApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CmsApiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public CmsApiClient(HttpClient httpClient, ILogger<CmsApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        #region Core HTTP Methods

        public async Task<ApiResult<LoginResponseData>> LoginAsync(LoginRequestModel request)
        {
            return await PostAsync<LoginRequestModel, LoginResponseData>("/api/auth/login", request);
        }

        public async Task<ApiResult<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);
                return await HandleResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi GET {Endpoint}", endpoint);
                return ApiResult<T>.Fail("Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.");
            }
        }

        public async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, data, _jsonOptions);
                return await HandleResponseAsync<TResponse>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
                return ApiResult<TResponse>.Fail("Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.");
            }
        }

        public async Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync(endpoint, data, _jsonOptions);
                return await HandleResponseAsync<TResponse>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi PUT {Endpoint}", endpoint);
                return ApiResult<TResponse>.Fail("Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.");
            }
        }

        public async Task<ApiResult<TResponse>> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var response = await _httpClient.PatchAsJsonAsync(endpoint, data, _jsonOptions);
                return await HandleResponseAsync<TResponse>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi PATCH {Endpoint}", endpoint);
                return ApiResult<TResponse>.Fail("Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.");
            }
        }

        public async Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string endpoint)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                return await HandleResponseAsync<TResponse>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi DELETE {Endpoint}", endpoint);
                return ApiResult<TResponse>.Fail("Không thể kết nối đến máy chủ API. Vui lòng thử lại sau.");
            }
        }

        #endregion

        #region Categories (Danh mục)

        public async Task<ApiResult<IEnumerable<CategoryDto>>> GetCategoriesAsync()
        {
            return await GetAsync<IEnumerable<CategoryDto>>("/api/categories");
        }

        public async Task<ApiResult<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto)
        {
            return await PostAsync<CreateCategoryDto, CategoryDto>("/api/categories", dto);
        }

        public async Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, UpdateCategoryDto dto)
        {
            return await PutAsync<UpdateCategoryDto, CategoryDto>($"/api/categories/{id}", dto);
        }

        public async Task<ApiResult<bool>> DeleteCategoryAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/categories/{id}");
                
                // Bắt riêng lỗi nghiệp vụ Phân hệ E: 409 (Conflict) hoặc 400 (Bad Request) khi danh mục đang chứa sách
                if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try
                        {
                            var errorResult = JsonSerializer.Deserialize<ApiResult<bool>>(content, _jsonOptions);
                            if (!string.IsNullOrWhiteSpace(errorResult?.Message))
                            {
                                return ApiResult<bool>.Fail(errorResult.Message);
                            }
                        }
                        catch { }
                    }
                    return ApiResult<bool>.Fail("Danh mục đang chứa sách, không thể xóa.");
                }

                return await HandleResponseAsync<bool>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi xóa danh mục ID {Id}", id);
                return ApiResult<bool>.Fail("Không thể kết nối đến máy chủ khi xóa danh mục.");
            }
        }

        #endregion

        #region Areas (Khu vực / Kệ sách)

        public async Task<ApiResult<IEnumerable<AreaDto>>> GetAreasAsync()
        {
            return await GetAsync<IEnumerable<AreaDto>>("/api/areas");
        }

        public async Task<ApiResult<AreaDto>> CreateAreaAsync(CreateAreaDto dto)
        {
            return await PostAsync<CreateAreaDto, AreaDto>("/api/areas", dto);
        }

        public async Task<ApiResult<AreaDto>> UpdateAreaAsync(int id, UpdateAreaDto dto)
        {
            return await PutAsync<UpdateAreaDto, AreaDto>($"/api/areas/{id}", dto);
        }

        public async Task<ApiResult<bool>> DeleteAreaAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/areas/{id}");

                // Bắt riêng lỗi nghiệp vụ Phân hệ E: 409 (Conflict) hoặc 400 (Bad Request) khi kệ sách đang có sách
                if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try
                        {
                            var errorResult = JsonSerializer.Deserialize<ApiResult<bool>>(content, _jsonOptions);
                            if (!string.IsNullOrWhiteSpace(errorResult?.Message))
                            {
                                return ApiResult<bool>.Fail(errorResult.Message);
                            }
                        }
                        catch { }
                    }
                    return ApiResult<bool>.Fail("Khu vực kệ đang chứa sách, không thể xóa.");
                }

                return await HandleResponseAsync<bool>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi xóa khu vực kệ ID {Id}", id);
                return ApiResult<bool>.Fail("Không thể kết nối đến máy chủ khi xóa khu vực kệ.");
            }
        }

        #endregion

        #region Books (Quản lý sách)

        public async Task<ApiResult<PaginatedResult<BookDto>>> GetBooksAsync(
            int page = 1,
            int pageSize = 12,
            string? keyword = null,
            int? categoryId = null,
            int? areaId = null)
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageIndex={page}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                queryParams.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                queryParams.Add($"categoryId={categoryId.Value}");
            }

            if (areaId.HasValue && areaId.Value > 0)
            {
                queryParams.Add($"areaId={areaId.Value}");
            }

            var endpoint = $"/api/books?{string.Join("&", queryParams)}";
            return await GetAsync<PaginatedResult<BookDto>>(endpoint);
        }

        public async Task<ApiResult<BookDto>> GetBookByIdAsync(int id)
        {
            return await GetAsync<BookDto>($"/api/books/{id}");
        }

        public async Task<ApiResult<BookDto>> GetBookByBarcodeAsync(string barcode)
        {
            return await GetAsync<BookDto>($"/api/books/barcode/{Uri.EscapeDataString(barcode.Trim())}");
        }

        public async Task<ApiResult<int>> CreateBookAsync(CreateBookDto dto)
        {
            return await PostAsync<CreateBookDto, int>("/api/books", dto);
        }

        public async Task<ApiResult<bool>> UpdateBookAsync(int id, UpdateBookDto dto)
        {
            return await PutAsync<UpdateBookDto, bool>($"/api/books/{id}", dto);
        }

        public async Task<ApiResult<bool>> DeleteBookAsync(int id)
        {
            return await DeleteAsync<bool>($"/api/books/{id}");
        }

        #endregion

        #region Suppliers (Nhà cung cấp - Phân hệ F)

        public async Task<ApiResult<IEnumerable<SupplierDto>>> GetSuppliersAsync()
        {
            return await GetAsync<IEnumerable<SupplierDto>>("/api/suppliers");
        }

        public async Task<ApiResult<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto)
        {
            return await PostAsync<CreateSupplierDto, SupplierDto>("/api/suppliers", dto);
        }

        public async Task<ApiResult<SupplierDto>> UpdateSupplierAsync(int id, UpdateSupplierDto dto)
        {
            return await PutAsync<UpdateSupplierDto, SupplierDto>($"/api/suppliers/{id}", dto);
        }

        public async Task<ApiResult<bool>> DeleteSupplierAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/suppliers/{id}");
                if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        try
                        {
                            var errorResult = JsonSerializer.Deserialize<ApiResult<bool>>(content, _jsonOptions);
                            if (!string.IsNullOrWhiteSpace(errorResult?.Message))
                            {
                                return ApiResult<bool>.Fail(errorResult.Message);
                            }
                        }
                        catch { }
                    }
                    return ApiResult<bool>.Fail("Nhà cung cấp đang có phiếu nhập liên kết, chỉ có thể vô hiệu hóa.");
                }

                return await HandleResponseAsync<bool>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi vô hiệu hóa nhà cung cấp ID {Id}", id);
                return ApiResult<bool>.Fail("Không thể kết nối đến máy chủ khi cập nhật nhà cung cấp.");
            }
        }

        #endregion

        #region Inventory Stock & Adjustment (Tồn kho & Kiểm kê)

        public async Task<ApiResult<IEnumerable<StockItemDto>>> GetStockAsync(bool lowOnly = false, string? keyword = null)
        {
            var queryParams = new List<string>
            {
                $"lowOnly={lowOnly.ToString().ToLower()}"
            };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                queryParams.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
            }

            var endpoint = $"/api/inventory/stock?{string.Join("&", queryParams)}";
            return await GetAsync<IEnumerable<StockItemDto>>(endpoint);
        }

        public async Task<ApiResult<bool>> AdjustStockAsync(AdjustStockDto dto)
        {
            return await PostAsync<AdjustStockDto, bool>("/api/inventory/adjust", dto);
        }

        #endregion

        #region Inventory Receipts (Phiếu nhập kho)

        public async Task<ApiResult<PaginatedResult<ImportReceiptDto>>> GetReceiptsAsync(
            int? supplierId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 10)
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageIndex={page}",
                $"pageSize={pageSize}"
            };

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                queryParams.Add($"supplierId={supplierId.Value}");
            }

            if (fromDate.HasValue)
            {
                queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            }

            if (toDate.HasValue)
            {
                queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
            }

            var endpoint = $"/api/inventory/receipts?{string.Join("&", queryParams)}";
            return await GetAsync<PaginatedResult<ImportReceiptDto>>(endpoint);
        }

        public async Task<ApiResult<ImportReceiptDetailDto>> GetReceiptByIdAsync(int id)
        {
            return await GetAsync<ImportReceiptDetailDto>($"/api/inventory/receipts/{id}");
        }

        public async Task<ApiResult<int>> CreateReceiptAsync(CreateImportReceiptDto dto)
        {
            return await PostAsync<CreateImportReceiptDto, int>("/api/inventory/receipts", dto);
        }

        #endregion

        #region Promotions (Khuyến mãi - Phân hệ J)

        public async Task<ApiResult<IEnumerable<PromotionDto>>> GetPromotionsAsync()
        {
            return await GetAsync<IEnumerable<PromotionDto>>("/api/promotions");
        }

        public async Task<ApiResult<IEnumerable<PromotionDto>>> GetActivePromotionsAsync()
        {
            var allResult = await GetPromotionsAsync();
            if (!allResult.Success || allResult.Data == null)
            {
                return allResult;
            }

            var activeList = allResult.Data
                .Where(p => p.IsEffectiveNow)
                .OrderByDescending(p => p.PromotionId)
                .ToList();

            return ApiResult<IEnumerable<PromotionDto>>.Ok(activeList);
        }

        public async Task<ApiResult<PromotionDto>> GetPromotionByIdAsync(int id)
        {
            var allResult = await GetPromotionsAsync();
            if (!allResult.Success || allResult.Data == null)
            {
                return ApiResult<PromotionDto>.Fail(allResult.Message ?? "Không thể lấy danh sách chương trình khuyến mãi.");
            }

            var promo = allResult.Data.FirstOrDefault(p => p.PromotionId == id);
            if (promo == null)
            {
                return ApiResult<PromotionDto>.Fail("Không tìm thấy chương trình khuyến mãi tương ứng.");
            }

            return ApiResult<PromotionDto>.Ok(promo);
        }

        public async Task<ApiResult<PromotionDto>> CreatePromotionAsync(CreatePromotionDto dto)
        {
            return await PostAsync<CreatePromotionDto, PromotionDto>("/api/promotions", dto);
        }

        public async Task<ApiResult<PromotionDto>> UpdatePromotionAsync(int id, UpdatePromotionDto dto)
        {
            return await PutAsync<UpdatePromotionDto, PromotionDto>($"/api/promotions/{id}", dto);
        }

        public async Task<ApiResult<bool>> TogglePromotionActiveAsync(int id, bool isActive)
        {
            // Gọi chính xác API Contract: PATCH /api/promotions/{id}/active
            var patchResult = await PatchAsync<object, bool>($"/api/promotions/{id}/active", new { isActive });
            if (patchResult.Success)
            {
                return patchResult;
            }

            // Fallback dự phòng tương thích nếu Backend route PATCH chưa đăng ký
            if (!isActive)
            {
                return await DeletePromotionAsync(id);
            }
            else
            {
                var promoResult = await GetPromotionByIdAsync(id);
                if (promoResult.Success && promoResult.Data != null)
                {
                    promoResult.Data.IsActive = true;
                    var putResult = await PutAsync<PromotionDto, PromotionDto>($"/api/promotions/{id}", promoResult.Data);
                    if (putResult.Success)
                    {
                        return ApiResult<bool>.Ok(true, "Kích hoạt chương trình khuyến mãi thành công.");
                    }
                }
            }

            return patchResult;
        }

        public async Task<ApiResult<bool>> DeletePromotionAsync(int id)
        {
            return await DeleteAsync<bool>($"/api/promotions/{id}");
        }

        #endregion

        #region Members (Thành viên & Tích điểm - Phân hệ I)

        public async Task<ApiResult<MemberDto>> GetMemberByPhoneAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return ApiResult<MemberDto>.Fail("Vui lòng nhập số điện thoại thành viên.");
            }

            var cleanPhone = phoneNumber.Trim();
            return await GetAsync<MemberDto>($"/api/members/{Uri.EscapeDataString(cleanPhone)}");
        }

        public async Task<ApiResult<IEnumerable<PointTransactionDto>>> GetPointHistoryAsync(int memberId)
        {
            return await GetAsync<IEnumerable<PointTransactionDto>>($"/api/members/{memberId}/point-history");
        }

        public async Task<ApiResult<MemberDto>> CreateMemberAsync(CreateMemberDto dto)
        {
            return await PostAsync<CreateMemberDto, MemberDto>("/api/members", dto);
        }

        public async Task<ApiResult<MemberDto>> UpdateMemberAsync(int id, UpdateMemberDto dto)
        {
            return await PutAsync<UpdateMemberDto, MemberDto>($"/api/members/{id}", dto);
        }

        #endregion

        #region Response Handler

        private async Task<ApiResult<T>> HandleResponseAsync<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return ApiResult<T>.Ok(default!);
                }

                try
                {
                    var result = JsonSerializer.Deserialize<ApiResult<T>>(content, _jsonOptions);
                    if (result != null) return result;
                }
                catch (JsonException)
                {
                    try
                    {
                        var directData = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                        return ApiResult<T>.Ok(directData!);
                    }
                    catch (Exception)
                    {
                        // Fallback
                    }
                }

                return ApiResult<T>.Ok(default!);
            }

            // Xử lý các mã lỗi HTTP chuẩn
            string errorMessage = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => "Dữ liệu yêu cầu không hợp lệ (400).",
                HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn hoặc không hợp lệ (401).",
                HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này (403).",
                HttpStatusCode.NotFound => "Không tìm thấy dữ liệu yêu cầu (404).",
                HttpStatusCode.Conflict => "Xung đột dữ liệu hoặc bản ghi đã tồn tại (409).",
                HttpStatusCode.InternalServerError => "Máy chủ API gặp sự cố nội bộ (500).",
                _ => $"Lỗi máy chủ HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };

            // Nếu server trả về message JSON chi tiết, ưu tiên lấy message đó
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var errorResult = JsonSerializer.Deserialize<ApiResult<T>>(content, _jsonOptions);
                    if (!string.IsNullOrWhiteSpace(errorResult?.Message))
                    {
                        errorMessage = errorResult.Message;
                    }
                }
                catch
                {
                    // Giữ nguyên errorMessage mặc định
                }
            }

            return ApiResult<T>.Fail(errorMessage);
        }

        #endregion
    }
}
