using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BookKiosk.CMS.Models.Promotions;

namespace BookKiosk.CMS.ViewModels.Promotions
{
    public class PromotionListViewModel
    {
        public IEnumerable<PromotionDto> Promotions { get; set; } = new List<PromotionDto>();
        public string? Keyword { get; set; }
        public string CurrentStatus { get; set; } = "all"; // all, active, upcoming, expired, inactive
        
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int UpcomingCount { get; set; }
        public int ExpiredCount { get; set; }
        public int InactiveCount { get; set; }

        public bool IsAdmin { get; set; }
    }

    public class ProductDiscountItemViewModel
    {
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public decimal DiscountAmount { get; set; } = 10000;
    }

    public class PromotionFormViewModel
    {
        public int PromotionId { get; set; }

        [Required(ErrorMessage = "Tên chương trình khuyến mãi không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên chương trình tối đa 255 ký tự")]
        [Display(Name = "Tên chương trình khuyến mãi")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả thể lệ tối đa 500 ký tự")]
        [Display(Name = "Mô tả thể lệ / Điều kiện")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại hình khuyến mãi")]
        [Display(Name = "Hình thức khuyến mãi")]
        public int PromotionType { get; set; } = 1; // 1 = Hóa đơn (OrderDiscount), 2 = Sản phẩm sách (ProductDiscount)

        [Required(ErrorMessage = "Ngày bắt đầu áp dụng không được để trống")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Ngày kết thúc áp dụng không được để trống")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

        public string? OriginalStartDateUtc { get; set; }
        public string? OriginalEndDateUtc { get; set; }

        [Display(Name = "Kích hoạt hiệu lực")]
        public bool IsActive { get; set; } = true;

        // --- Cấu hình cho Loại 1: Giảm theo Hóa đơn (OrderDiscount) ---
        [Display(Name = "Giá trị đơn hàng tối thiểu (VNĐ)")]
        public decimal? OrderMinOrderValue { get; set; } = 0;

        [Display(Name = "Mức giảm đơn hàng (VNĐ)")]
        public decimal? OrderDiscountAmount { get; set; } = 10000;

        // --- Cấu hình cho Loại 2: Giảm theo Sách (ProductDiscount) ---
        public List<ProductDiscountItemViewModel> ProductDiscounts { get; set; } = new();

        public bool IsEditMode => PromotionId > 0;
    }
}
