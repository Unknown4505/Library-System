namespace BookKiosk.Domain.Entities;

public class PromotionProductDiscount
{
    public int PromotionId { get; set; }
    public int BookId { get; set; }
    public decimal DiscountAmount { get; set; }

    public Promotion? Promotion { get; set; }
    public Book? Book { get; set; }
}