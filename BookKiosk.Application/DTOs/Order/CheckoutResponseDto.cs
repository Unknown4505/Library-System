namespace BookKiosk.Application.DTOs.Order;

public class CheckoutResponseDto
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsedAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string SepayQrCodeUrl { get; set; } = string.Empty;
}
