using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookKiosk.CMS.Models.Api.Pos
{
    // --- 1. DTO Sách (Khớp 100% BookDto của Backend) ---
    public class PosBookDto
    {
        public int BookId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? Publisher { get; set; }
        public string? ImageUrl { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int AvailableStock { get; set; }
        public bool IsActive { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int? AreaId { get; set; }
        public string? AreaName { get; set; }
    }

    // --- 2. DTO Thành viên (Khớp MemberDto của Backend) ---
    public class PosMemberDto
    {
        public int MemberId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Points { get; set; }

        public decimal PointMoneyValue => Points * 1000m;
        public string FormattedPoints => Points.ToString("N0") + " điểm";
    }

    public class PosCreateMemberDto
    {
        [Required(ErrorMessage = "Số điện thoại thành viên không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng di động Việt Nam (10 số)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên thành viên không được để trống")]
        [MaxLength(255, ErrorMessage = "Họ và tên tối đa 255 ký tự")]
        public string FullName { get; set; } = string.Empty;
    }

    // --- 3. DTO Danh mục (Khớp CategoryDto của Backend) ---
    public class PosCategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // --- 4. DTO Khuyến mãi (Khớp PromotionDto Backend & Hỗ trợ OrderDiscount) ---
    public class PosPromotionDto
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }

        // Trường mở rộng cho OrderDiscount (tự động bind khi Backend expose)
        public decimal? MinOrderValue { get; set; }
        public decimal DiscountAmount { get; set; }

        public bool IsEffectiveNow => IsActive && DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;
        public string SummaryText => !string.IsNullOrWhiteSpace(Description) 
            ? Description 
            : (MinOrderValue.HasValue ? $"Giảm {DiscountAmount:N0} ₫ cho đơn từ {MinOrderValue.Value:N0} ₫" : $"Giảm {DiscountAmount:N0} ₫");
    }

    // --- 5. DTO Tạo đơn Counter (Checkout) ---
    public class CounterCheckoutRequestDto
    {
        public int? MemberId { get; set; }
        public int? PromotionId { get; set; }
        public int PointsToUse { get; set; } = 0;
        public string PaymentMethod { get; set; } = "Cash"; // "Cash" hoặc "QR"
        public List<CounterCheckoutItemDto> Items { get; set; } = new();
    }

    public class CounterCheckoutItemDto
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
    }

    public class CounterCheckoutResponseDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;

        [JsonConverter(typeof(BookKiosk.CMS.Models.Common.FlexibleEnumConverter<BookKiosk.CMS.Models.Api.Orders.OrderStatus>))]
        public BookKiosk.CMS.Models.Api.Orders.OrderStatus OrderStatus { get; set; } = BookKiosk.CMS.Models.Api.Orders.OrderStatus.Pending;

        public decimal SubTotal { get; set; }
        public int? PromotionId { get; set; }
        public string? PromotionName { get; set; }
        public BookKiosk.Domain.Enums.PromotionType? PromotionType { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsUsed { get; set; }
        public decimal PointsUsedAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? SepayQrCodeUrl { get; set; }
        public DateTime? ExpiresAt { get; set; } // UTC có hậu tố Z
        public DateTime CreatedAt { get; set; }
        public DateTime? ServerTime { get; set; }
        public int? RemainingSeconds { get; set; }
        public List<CounterOrderItemDto>? Items { get; set; }

        [JsonIgnore]
        public string StatusString => OrderStatus.ToString();
    }

    public class CounterOrderItemDto
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPriceAtTime { get; set; }
        public decimal DiscountPerUnit { get; set; }
        public decimal LineTotal { get; set; }
        public decimal LineDiscountAmount { get; set; }
        public decimal NetLineTotal { get; set; }
    }

    // --- 6. DTO Trạng thái đơn (Polling Status) ---
    public class OrderPaymentStatusDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;

        [JsonConverter(typeof(BookKiosk.CMS.Models.Common.FlexibleEnumConverter<BookKiosk.CMS.Models.Api.Orders.OrderStatus>))]
        public BookKiosk.CMS.Models.Api.Orders.OrderStatus Status { get; set; } = BookKiosk.CMS.Models.Api.Orders.OrderStatus.Pending;

        public DateTime? CompletedAt { get; set; }
        public DateTime? ServerTime { get; set; }

        [JsonIgnore]
        public string StatusString => Status.ToString();

        [JsonIgnore]
        public bool IsPaid => Status == BookKiosk.CMS.Models.Api.Orders.OrderStatus.Paid;

        [JsonIgnore]
        public bool IsCancelled => Status == BookKiosk.CMS.Models.Api.Orders.OrderStatus.Cancelled;
    }

    // --- 7. DTO Hóa đơn in nhiệt K80 ---
    public class PosReceiptDto
    {
        public string OrderCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string MemberDisplayName { get; set; } = "Thành viên"; // Tuyệt đối không SĐT, không in tên trừ khi cấu hình
        public int? PointsEarned { get; set; }
        public List<PosReceiptItemDto> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public int PointsUsed { get; set; }
        public decimal PointsUsedAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";

        // Thông tin cửa hàng (được cấu hình từ appsettings)
        public string StoreName { get; set; } = "NHÀ SÁCH BOOKKIOSK";
        public string StoreAddress { get; set; } = "Điểm bán lẻ Kiosk & Quầy thu ngân";
        public string StoreHotline { get; set; } = "1900 6868";
        public string StoreWebsite { get; set; } = "bookkiosk.vn";
    }

    public class PosReceiptItemDto
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
