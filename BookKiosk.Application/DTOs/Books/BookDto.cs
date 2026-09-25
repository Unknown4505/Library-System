namespace BookKiosk.Application.DTOs.Books;

public class BookDto
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
    public int AvailableStock { get; set; } // Tính toán sẵn để FE dùng
    
    public bool IsActive { get; set; }

    // Thông tin Danh mục và Khu vực (Lấy kèm theo để Frontend hiển thị chữ thay vì chỉ hiển thị ID)
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
}
