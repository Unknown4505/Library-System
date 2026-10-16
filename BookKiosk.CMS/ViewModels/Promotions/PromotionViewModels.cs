using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        [Display(Name = "Kích hoạt hiệu lực")]
        public bool IsActive { get; set; } = true;

        // --- Cấu hình cho Loại 1: Giảm theo Hóa đơn (OrderDiscount) ---
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn tối thiểu phải từ 0 ₫ trở lên")]
        [Display(Name = "Giá trị đơn hàng tối thiểu (VNĐ)")]
        public decimal? MinOrderValue { get; set; } = 0;

        // --- Cấu hình Mức giảm cố định VNĐ (áp dụng cho cả 2 loại) ---
        [Required(ErrorMessage = "Số tiền giảm giá không được để trống")]
        [Range(1000, 100000000, ErrorMessage = "Mức giảm cố định phải từ 1.000 ₫ đến 100.000.000 ₫")]
        [Display(Name = "Số tiền giảm cố định (VNĐ)")]
        public decimal DiscountAmount { get; set; } = 10000;

        // --- Cấu hình cho Loại 2: Giảm theo Sách (ProductDiscount) ---
        [Display(Name = "Ấn phẩm sách áp dụng")]
        public int? BookId { get; set; }

        public IEnumerable<SelectListItem> AvailableBooks { get; set; } = new List<SelectListItem>();

        public bool IsEditMode => PromotionId > 0;
    }
}
