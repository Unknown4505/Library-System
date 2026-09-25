using System.ComponentModel.DataAnnotations;

namespace BookKiosk.Application.DTOs.Books;

public class UpdateBookDto
{
    [Required(ErrorMessage = "Tên sách không được để trống")]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên tác giả không được để trống")]
    [MaxLength(255)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Publisher { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }
    
    [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
    public decimal CostPrice { get; set; }
    
    [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
    public decimal SellingPrice { get; set; }
    
    [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không hợp lệ")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    public int CategoryId { get; set; }

    public int? AreaId { get; set; }
    
    public bool IsActive { get; set; }
}
