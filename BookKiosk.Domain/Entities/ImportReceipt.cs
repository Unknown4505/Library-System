namespace BookKiosk.Domain.Entities;

public class ImportReceipt : BaseEntity
{
    public int ImportReceiptId { get; set; }
    public int SupplierId { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public Supplier? Supplier { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<ImportReceiptDetail> ImportReceiptDetails { get; set; } = new List<ImportReceiptDetail>();
}