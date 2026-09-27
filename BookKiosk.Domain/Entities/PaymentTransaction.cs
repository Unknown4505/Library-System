namespace BookKiosk.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public int TransactionId { get; set; }
    public int OrderId { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Gateway { get; set; } = string.Empty;

    public Order? Order { get; set; }
}