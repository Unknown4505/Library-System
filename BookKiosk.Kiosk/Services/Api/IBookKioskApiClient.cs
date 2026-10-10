using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services.Api;

/// <summary>
/// Contract đầy đủ của Kiosk Client — tất cả API call của Kiosk đều đi qua đây.
/// FE chỉ phụ thuộc vào Interface này, không bao giờ tạo HttpClient trực tiếp.
/// </summary>
public interface IBookKioskApiClient
{
    // ── BOOKS ────────────────────────────────────────────────────────────────

    /// <summary>Lấy danh sách sách có phân trang + tìm kiếm + filter (dùng cho SearchPage)</summary>
    Task<ApiResponseDto<PaginatedResultDto<BookDto>>?> GetBooksAsync(
        int page = 1,
        int pageSize = 12,
        string? keyword = null,
        int? categoryId = null);

    /// <summary>Lấy chi tiết sách theo mã vạch (dùng cho máy quét hoặc nhập tay)</summary>
    Task<ApiResponseDto<BookDto>?> GetBookByBarcodeAsync(string barcode);

    /// <summary>Lấy chi tiết sách theo ID (dùng cho BookDetailPage)</summary>
    Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId);

    // ── CATEGORIES & AREAS ───────────────────────────────────────────────────

    /// <summary>Lấy danh sách danh mục (dùng cho filter dropdown ở SearchPage)</summary>
    Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync();

    /// <summary>Lấy danh sách khu vực kệ (dùng để hiển thị vị trí kệ ở BookDetailPage)</summary>
    Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync();

    // ── MEMBERS ───────────────────────────────────────────────────────────────

    /// <summary>Tra cứu thành viên theo SĐT (dùng khi nhập điểm quy đổi ở CartPage)</summary>
    Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber);

    /// <summary>Tạo thành viên mới nếu chưa có (đăng ký ngay tại Kiosk)</summary>
    Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request);

    // ── ORDERS & CHECKOUT ────────────────────────────────────────────────────

    /// <summary>Tạo đơn hàng + giữ kho + trả về link QR VietQR để hiển thị ở PaymentPage</summary>
    Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request);

    /// <summary>Huỷ đơn (khi hết timeout đếm ngược ở PaymentPage)</summary>
    Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId);

    // ── PAYMENT POLLING ───────────────────────────────────────────────────────

    /// <summary>
    /// Kiosk polling mỗi 3 giây — kiểm tra đơn đã được thanh toán chưa (task 7.5 / 7.6)
    /// </summary>
    Task<ApiResponseDto<PaymentStatusDto>?> GetPaymentStatusAsync(int orderId);

    // ── KIOSK HEARTBEAT ───────────────────────────────────────────────────────

    /// <summary>Gửi heartbeat theo contract chuẩn dùng ID số trong database.</summary>
    Task<ApiResponseDto<object>?> SendHeartbeatAsync(int kioskId, int status = 1, string? errorCode = null, string? errorMessage = null);

    /// <summary>
    /// Overload tương thích với caller cũ. Giá trị phải là chuỗi biểu diễn ID số,
    /// không phải mã hiển thị như KIOSK-01.
    /// </summary>
    Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId);
}
