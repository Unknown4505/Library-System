namespace BookKiosk.Application.DTOs.Order;

public class CheckoutRequestDto
{
    public int? MemberId { get; set; }
    public int PointsToUse { get; set; }
    public List<CheckoutItemDto> Items { get; set; } = new();
}

public class CheckoutItemDto
{
    public int BookId { get; set; }
    public int Quantity { get; set; }
}
