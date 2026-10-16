using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Models.Api.Orders
{
    public enum OrderStatus
    {
        Pending = 1,
        Paid = 2,
        Cancelled = 3
    }

    public enum SaleChannel
    {
        Kiosk = 1,
        Counter = 2
    }

    public enum PaymentMethod
    {
        Cash = 1,
        QR = 2
    }

    public class OrderFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }       // Đã bao gồm hết ngày 23:59:59.999 VN
        public OrderStatus? Status { get; set; }
        public SaleChannel? Channel { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public string? OrderCode { get; set; }      // CHỈ tìm kiếm theo mã đơn hàng ORD-yyyyMMdd-XXXX
        public bool? NeedsSupportOnly { get; set; } // Lọc riêng cho tab "Cần hỗ trợ"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;     // Tối đa 100
    }

    public class OrderListItemDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty; // Format: ORD-yyyyMMdd-XXXX
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [JsonConverter(typeof(FlexibleEnumConverter<SaleChannel>))]
        public SaleChannel SaleChannel { get; set; } = SaleChannel.Counter;

        [JsonConverter(typeof(FlexibleEnumConverter<OrderStatus>))]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

        [JsonConverter(typeof(FlexibleEnumConverter<PaymentMethod>))]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsUsed { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CashierName { get; set; }
        public bool HasMember { get; set; }

        // Nhận trực tiếp từ danh sách Backend (Chống lỗi N+1). Null nếu Backend chưa hỗ trợ.
        public bool? NeedsSupport { get; set; }
    }

    public class OrderDetailDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [JsonConverter(typeof(FlexibleEnumConverter<SaleChannel>))]
        public SaleChannel SaleChannel { get; set; } = SaleChannel.Counter;

        [JsonConverter(typeof(FlexibleEnumConverter<OrderStatus>))]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

        [JsonConverter(typeof(FlexibleEnumConverter<PaymentMethod>))]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? PromotionName { get; set; }
        public int PointsUsed { get; set; }
        public decimal PointsUsedAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CashierName { get; set; }

        // Khách hàng & SĐT nguyên bản từ API Backend
        public string? MemberName { get; set; }
        public string? MemberPhone { get; set; } // Nhận đúng từ Backend, PhoneHelper sẽ che khi hiển thị
        public int? PointsEarned { get; set; }   // CHỈ hiển thị nếu Backend trả

        // Snapshot dòng hàng tại thời điểm mua:
        public List<OrderDetailItemDto> Items { get; set; } = new();

        // Lịch sử giao dịch thanh toán để đối soát:
        public List<OrderPaymentTransactionDto> Payments { get; set; } = new();

        // Thuộc tính nhận diện đơn cần hỗ trợ tại trang chi tiết:
        // Đơn Pending hoặc Cancelled nhưng đã có payment (Amount > 0) mà đơn không Paid
        public bool IsPaymentMismatch => OrderStatus != OrderStatus.Paid 
            && Payments != null 
            && Payments.Any(p => p.Amount > 0);
    }

    public class OrderDetailItemDto
    {
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPriceAtTime { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class OrderPaymentTransactionDto
    {
        public int TransactionId { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Gateway { get; set; } = "SePay";
        public DateTime CreatedAt { get; set; }
    }
}
