using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    public interface IOrderApi
    {
        /// <summary>
        /// Lấy danh sách hóa đơn đơn hàng theo bộ lọc và phân trang (docs/04 §3.3)
        /// </summary>
        Task<ApiResult<PaginatedResult<OrderListItemDto>>> GetOrdersAsync(OrderFilterDto filter);

        /// <summary>
        /// Lấy chi tiết đơn hàng (kèm snapshot sách và danh sách giao dịch thanh toán đối soát)
        /// </summary>
        Task<ApiResult<OrderDetailDto>> GetOrderByIdAsync(int orderId);

        /// <summary>
        /// In lại hóa đơn K80 cho đơn hàng đã thanh toán thành công (OrderStatus = Paid)
        /// Không để lộ SĐT khách hàng
        /// </summary>
        Task<ApiResult<PosReceiptDto>> GetOrderReceiptAsync(int orderId);

        /// <summary>
        /// Lấy số lượng đơn hàng Cần hỗ trợ (Pending/Cancelled có payment mà chưa Paid) cho chuông thông báo
        /// </summary>
        Task<ApiResult<int>> GetNeedsSupportCountAsync();
    }
}
