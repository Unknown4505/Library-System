namespace BookKiosk.Domain.Entities;

public class PromotionOrderDiscount
{
    public int PromotionId { get; set; }
    public decimal MinOrderValue { get; set; }
    public decimal DiscountAmount { get; set; }

    public Promotion? Promotion { get; set; }
}