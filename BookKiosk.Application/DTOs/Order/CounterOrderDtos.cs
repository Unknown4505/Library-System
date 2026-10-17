using BookKiosk.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BookKiosk.Application.DTOs.Order;

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
    [Required]
    public int BookId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
    public int Quantity { get; set; }
}

public class CounterCheckoutResponseDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? SepayQrCodeUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ServerTime { get; set; }
    public int? RemainingSeconds { get; set; }
    public List<CounterOrderItemDto> Items { get; set; } = new();
}

public class CounterOrderItemDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal LineTotal { get; set; }
}

public class PosReceiptDto
{
    public string OrderCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string MemberDisplayName { get; set; } = "Thành viên";
    public int? PointsEarned { get; set; }
    public List<PosReceiptItemDto> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";

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

public class OrderFilterDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public OrderStatus? Status { get; set; }
    public SaleChannel? Channel { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Keyword { get; set; }
    public string? OrderCode { get; set; }
    public bool? NeedsSupportOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class OrderListItemDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SaleChannel SaleChannel { get; set; } = SaleChannel.Counter;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal TotalAmount { get; set; }
    public string? CashierName { get; set; }
    public bool HasMember { get; set; }
    public bool NeedsSupport { get; set; }
}

public class OrderDetailDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SaleChannel SaleChannel { get; set; } = SaleChannel.Counter;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

    // Phục vụ polling quầy POS (CmsPosApi.GetOrderStatusAsync deserialize vào OrderPaymentStatusDto)
    public OrderStatus Status
    {
        get => OrderStatus;
        set => OrderStatus = value;
    }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionName { get; set; }
    public int PointsUsed { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? CashierName { get; set; }

    public string? MemberName { get; set; }
    public string? MemberPhone { get; set; }
    public int? PointsEarned { get; set; }
    public DateTime? ServerTime { get; set; } = DateTime.UtcNow;

    public List<OrderDetailItemDto> Items { get; set; } = new();
    public List<OrderPaymentTransactionDto> Payments { get; set; } = new();

    public bool IsPaymentMismatch => OrderStatus != OrderStatus.Paid 
        && Payments != null 
        && Payments.Exists(p => p.Amount > 0);
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
