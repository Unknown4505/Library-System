using System.Collections.Generic;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    public interface IPosApi
    {
        // 1. Quét mã vạch (API thật CÓ)
        Task<ApiResult<PosBookDto>> GetBookByBarcodeAsync(string barcode);

        // 2. Tìm kiếm sách thủ công (API thật CÓ)
        Task<ApiResult<PaginatedResult<PosBookDto>>> SearchBooksAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 12);

        // 3. Lấy danh mục sách (API thật CÓ)
        Task<ApiResult<IEnumerable<PosCategoryDto>>> GetCategoriesAsync();

        // 4. Lấy danh sách Khuyến mãi (API thật CÓ /api/promotions)
        Task<ApiResult<IEnumerable<PosPromotionDto>>> GetActivePromotionsAsync();

        // 5. Tra cứu thành viên bằng SĐT (API thật CÓ /api/members/{phoneNumber})
        Task<ApiResult<PosMemberDto>> GetMemberByPhoneAsync(string phoneNumber);

        // 6. Đăng ký nhanh thành viên tại quầy (API thật CÓ POST /api/members)
        Task<ApiResult<PosMemberDto>> CreateMemberAsync(PosCreateMemberDto dto);

        // 7. Tạo đơn hàng quầy nháp (Backend CHƯA CÓ - Contract docs/04)
        Task<ApiResult<CounterCheckoutResponseDto>> CheckoutCounterAsync(CounterCheckoutRequestDto dto);

        // 8. Xác nhận thanh toán quầy (Backend CHƯA CÓ - Contract docs/04)
        Task<ApiResult<PosReceiptDto>> ConfirmPaymentAsync(int orderId);

        // 9. Hủy đơn quầy nhả kho (Backend CHƯA CÓ - Contract docs/04)
        Task<ApiResult<bool>> CancelOrderAsync(int orderId);

        // 10. Tra cứu trạng thái đơn JWT (Backend CHƯA CÓ - Contract docs/04)
        Task<ApiResult<OrderPaymentStatusDto>> GetOrderStatusAsync(int orderId);
    }
}
