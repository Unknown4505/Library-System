namespace BookKiosk.Domain.Entities;

public class ImportReceiptDetail
{
    public int ImportReceiptId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }

    public ImportReceipt? ImportReceipt { get; set; }
    public Book? Book { get; set; }
}