namespace BookKiosk.Application.DTOs.Promotions;

public sealed class PromotionOrderDiscountDto
{
    public decimal MinOrderValue { get; set; }
    public decimal DiscountAmount { get; set; }
}

public sealed class PromotionProductDiscountDto
{
    public int BookId { get; set; }
    public decimal DiscountAmount { get; set; }
}
