using BookKiosk.Domain.Enums;
using System.Text.Json.Serialization;

namespace BookKiosk.Application.DTOs.Order;

/// <summary>
/// Server-owned receipt data for a paid order. Values must be loaded from
/// Orders and OrderDetails; clients must not provide receipt totals or lines.
/// </summary>
public sealed class ReceiptDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<ReceiptLineDto> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public int? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public PromotionType? PromotionType { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentMethod PaymentMethod { get; set; }
}

/// <summary>A single persisted order line exposed on a paid receipt.</summary>
public sealed class ReceiptLineDto
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal DiscountPerUnit { get; set; }
    public decimal LineTotal { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public decimal NetLineTotal { get; set; }
}
