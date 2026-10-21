using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Order;

public class CheckoutResponseDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public int? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public PromotionType? PromotionType { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string SepayQrCodeUrl { get; set; } = string.Empty;
    public List<CheckoutItemDiscountDto> Items { get; set; } = new();
}

public sealed class CheckoutItemDiscountDto
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
