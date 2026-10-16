using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Inventory
{
    public class StockItemDto
    {
        public int BookId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? AreaName { get; set; }

        public int StockQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int AvailableStock { get; set; }

        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsActive { get; set; }

        public bool IsLowStock => AvailableStock <= 5;
    }

    public class AdjustStockDto
    {
        [Required(ErrorMessage = "Mã sách không được để trống")]
        public int BookId { get; set; }

        [Required(ErrorMessage = "Số lượng tồn thực tế không được để trống")]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn thực tế phải lớn hơn hoặc bằng 0")]
        [Display(Name = "Số lượng tồn thực tế kiểm đếm")]
        public int ActualQuantity { get; set; }

        [Required(ErrorMessage = "Lý do kiểm kê / điều chỉnh không được để trống")]
        [MaxLength(500, ErrorMessage = "Lý do điều chỉnh tối đa 500 ký tự")]
        [Display(Name = "Lý do kiểm kê / điều chỉnh (Bắt buộc)")]
        public string Reason { get; set; } = string.Empty;
    }
}
