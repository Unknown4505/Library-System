namespace BookKiosk.Domain.Entities;

public class Book : BaseEntity
{
    public int BookId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public string? ImageUrl { get; set; }
    
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Tồn kho khả dụng (AvailableStock) được tính toán động (NotMapped), 
    // nhưng ở đây ta chỉ dùng Getter để tiện lấy trong code, 
    // còn khi truy vấn DB (LINQ) thì EF Core sẽ tính toán nếu cần.
    public int AvailableStock => StockQuantity - ReservedQuantity;

    // Foreign Keys
    public int CategoryId { get; set; }
    public int? AreaId { get; set; }

    // Navigation Properties
    public Category? Category { get; set; }
    public Area? Area { get; set; }
}
