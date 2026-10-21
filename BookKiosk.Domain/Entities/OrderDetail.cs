namespace BookKiosk.Domain.Entities;

public class OrderDetail
{
    public int OrderId { get; set; }
    public int BookId { get; set; }
    public string? BookTitleAtTime { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal DiscountPerUnitAtTime { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    public Order? Order { get; set; }
    public Book? Book { get; set; }
}
