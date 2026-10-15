namespace BookKiosk.Kiosk.Models
{
    public class ReceiptParameter
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PromotionDiscount { get; set; }
        public decimal PointsDiscount { get; set; }
        public decimal SubTotal { get; set; }
    }
}
