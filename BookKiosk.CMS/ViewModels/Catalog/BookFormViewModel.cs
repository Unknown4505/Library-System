using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookKiosk.CMS.ViewModels.Catalog
{
    public class BookFormViewModel
    {
        public bool IsEditMode { get; set; }
        public int BookId { get; set; }

        [Required(ErrorMessage = "Mã vạch không được để trống")]
        [MaxLength(50, ErrorMessage = "Mã vạch tối đa 50 ký tự")]
        [Display(Name = "Mã vạch (Barcode / ISBN)")]
        public string Barcode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên sách không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên sách tối đa 255 ký tự")]
        [Display(Name = "Tên sách")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên tác giả không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên tác giả tối đa 255 ký tự")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "Nhà xuất bản tối đa 255 ký tự")]
        [Display(Name = "Nhà xuất bản")]
        public string? Publisher { get; set; }

        [MaxLength(500, ErrorMessage = "Đường dẫn hình ảnh tối đa 500 ký tự")]
        [Display(Name = "Hình ảnh bìa sách (URL)")]
        public string? ImageUrl { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0")]
        [Display(Name = "Giá nhập (VNĐ)")]
        public decimal CostPrice { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0")]
        [Display(Name = "Giá bán (VNĐ)")]
        public decimal SellingPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không hợp lệ")]
        [Display(Name = "Tồn kho thực tế")]
        public int StockQuantity { get; set; }

        [Display(Name = "Đang giữ chỗ (Reserved)")]
        public int ReservedQuantity { get; set; }

        [Display(Name = "Khả dụng bán (Available)")]
        public int AvailableStock => Math.Max(0, StockQuantity - ReservedQuantity);

        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục")]
        [Display(Name = "Danh mục thể loại")]
        public int CategoryId { get; set; }

        [Display(Name = "Khu vực / Kệ sách")]
        public int? AreaId { get; set; }

        [Display(Name = "Trạng thái kinh doanh")]
        public bool IsActive { get; set; } = true;

        // Select lists
        public List<SelectListItem> CategoryOptions { get; set; } = new();
        public List<SelectListItem> AreaOptions { get; set; } = new();
    }
}
