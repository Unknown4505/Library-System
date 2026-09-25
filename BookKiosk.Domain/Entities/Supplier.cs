namespace BookKiosk.Domain.Entities;

public class Supplier : BaseEntity
{
    public int SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ImportReceipt> ImportReceipts { get; set; } = new List<ImportReceipt>();
}