using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Catalog
{
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
        public int AvailableStock { get; set; }
        
        public bool IsActive { get; set; }

        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public int? AreaId { get; set; }
        public string? AreaName { get; set; }
    }

    public class CreateBookDto
    {
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
        
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho ban đầu không hợp lệ")]
        [Display(Name = "Số lượng tồn kho ban đầu")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục thể loại")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục thể loại")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Display(Name = "Khu vực / Kệ sách")]
        public int? AreaId { get; set; }
        
        [Display(Name = "Đang kinh doanh")]
        public bool IsActive { get; set; } = true;
    }

    public class UpdateBookDto
    {
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
        
        [Display(Name = "Tồn kho thực tế (Chỉ đọc)")]
        public int StockQuantity { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục thể loại")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục thể loại")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Display(Name = "Khu vực / Kệ sách")]
        public int? AreaId { get; set; }
        
        [Display(Name = "Trạng thái hiển thị / Kinh doanh")]
        public bool IsActive { get; set; }
    }
}
