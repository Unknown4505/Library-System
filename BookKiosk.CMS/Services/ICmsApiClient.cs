using BookKiosk.CMS.Models.Auth;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Models.Promotions;
using BookKiosk.CMS.Models.Members;

namespace BookKiosk.CMS.Services
{
    public interface ICmsApiClient
    {
        // --- Core HTTP Methods ---
        Task<ApiResult<LoginResponseData>> LoginAsync(LoginRequestModel request);
        Task<ApiResult<T>> GetAsync<T>(string endpoint);
        Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<ApiResult<TResponse>> PatchAsync<TRequest, TResponse>(string endpoint, TRequest data);
        Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string endpoint);

        // --- Categories (Danh mục thể loại) ---
        Task<ApiResult<IEnumerable<CategoryDto>>> GetCategoriesAsync();
        Task<ApiResult<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto);
        Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int id, UpdateCategoryDto dto);
        Task<ApiResult<bool>> DeleteCategoryAsync(int id);

        // --- Areas (Khu vực / Kệ sách) ---
        Task<ApiResult<IEnumerable<AreaDto>>> GetAreasAsync();
        Task<ApiResult<AreaDto>> CreateAreaAsync(CreateAreaDto dto);
        Task<ApiResult<AreaDto>> UpdateAreaAsync(int id, UpdateAreaDto dto);
        Task<ApiResult<bool>> DeleteAreaAsync(int id);

        // --- Books (Quản lý sách Catalog & Barcode Scanner) ---
        Task<ApiResult<PaginatedResult<BookDto>>> GetBooksAsync(int page = 1, int pageSize = 12, string? keyword = null, int? categoryId = null, int? areaId = null);
        Task<ApiResult<BookDto>> GetBookByIdAsync(int id);
        Task<ApiResult<BookDto>> GetBookByBarcodeAsync(string barcode);
        Task<ApiResult<int>> CreateBookAsync(CreateBookDto dto);
        Task<ApiResult<bool>> UpdateBookAsync(int id, UpdateBookDto dto);
        Task<ApiResult<bool>> DeleteBookAsync(int id);

        // --- Suppliers (Nhà cung cấp - Phân hệ F) ---
        Task<ApiResult<IEnumerable<SupplierDto>>> GetSuppliersAsync();
        Task<ApiResult<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto);
        Task<ApiResult<SupplierDto>> UpdateSupplierAsync(int id, UpdateSupplierDto dto);
        Task<ApiResult<bool>> DeleteSupplierAsync(int id);

        // --- Inventory Stock & Adjustment (Tồn kho & Kiểm kê) ---
        Task<ApiResult<IEnumerable<StockItemDto>>> GetStockAsync(bool lowOnly = false, string? keyword = null);
        Task<ApiResult<bool>> AdjustStockAsync(AdjustStockDto dto);

        // --- Inventory Receipts (Phiếu nhập kho) ---
        Task<ApiResult<PaginatedResult<ImportReceiptDto>>> GetReceiptsAsync(int? supplierId = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 10);
        Task<ApiResult<ImportReceiptDetailDto>> GetReceiptByIdAsync(int id);
        Task<ApiResult<int>> CreateReceiptAsync(CreateImportReceiptDto dto);

        // --- Promotions (Khuyến mãi - Phân hệ J) ---
        Task<ApiResult<IEnumerable<PromotionDto>>> GetPromotionsAsync();
        Task<ApiResult<IEnumerable<PromotionDto>>> GetActivePromotionsAsync();
        Task<ApiResult<PromotionDto>> GetPromotionByIdAsync(int id);
        Task<ApiResult<PromotionDto>> CreatePromotionAsync(CreatePromotionDto dto);
        Task<ApiResult<PromotionDto>> UpdatePromotionAsync(int id, UpdatePromotionDto dto);
        Task<ApiResult<bool>> TogglePromotionActiveAsync(int id, bool isActive);
        Task<ApiResult<bool>> DeletePromotionAsync(int id);

        // --- Members (Thành viên & Tích điểm - Phân hệ I) ---
        Task<ApiResult<MemberDto>> GetMemberByPhoneAsync(string phoneNumber);
        Task<ApiResult<IEnumerable<PointTransactionDto>>> GetPointHistoryAsync(int memberId);
        Task<ApiResult<MemberDto>> CreateMemberAsync(CreateMemberDto dto);
        Task<ApiResult<MemberDto>> UpdateMemberAsync(int id, UpdateMemberDto dto);
    }
}
