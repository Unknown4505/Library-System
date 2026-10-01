using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Order;

/// <summary>
/// DTO trả về trạng thái thanh toán — Kiosk dùng để polling mỗi 3 giây
/// </summary>
public class PaymentStatusDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái đơn hàng khớp với <see cref="OrderStatus"/>: Pending | Paid | Cancelled
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Đã thanh toán thành công chưa</summary>
    public bool IsPaid => Status == nameof(OrderStatus.Paid);

    /// <summary>Đơn đã bị huỷ (do timeout hoặc người dùng thoát)</summary>
    public bool IsCancelled => Status == nameof(OrderStatus.Cancelled);
}

