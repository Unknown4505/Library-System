using System;
using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Promotions
{
    public class PromotionDto
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PromotionType { get; set; } = 1; // 1 = OrderDiscount, 2 = ProductDiscount
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);
        public bool IsActive { get; set; } = true;

        // Chi tiết điều kiện giảm giá (Số tiền cố định VNĐ)
        public decimal? MinOrderValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? BookId { get; set; }
        public string? BookTitle { get; set; }

        // Helper getters phục vụ UI
        public string TypeName => PromotionType == 2 
            ? "Giảm giá từng cuốn sách (ProductDiscount)" 
            : "Giảm giá trên hóa đơn (OrderDiscount)";

        public bool IsEffectiveNow => IsActive && DateTime.Now >= StartDate && DateTime.Now <= EndDate;

        public string DisplayStatus
        {
            get
            {
                if (!IsActive) return "Đã tạm ngưng";
                if (DateTime.Now < StartDate) return "Sắp diễn ra";
                if (DateTime.Now > EndDate) return "Đã hết hạn";
                return "Đang hiệu lực";
            }
        }

        public string DisplayBadgeClass
        {
            get
            {
                if (!IsActive) return "bg-secondary-subtle text-secondary border border-secondary-subtle";
                if (DateTime.Now < StartDate) return "bg-info-subtle text-info border border-info-subtle";
                if (DateTime.Now > EndDate) return "bg-dark-subtle text-muted border";
                return "bg-success-subtle text-success border border-success-subtle";
            }
        }

        public string AutoSummary
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Description)) return Description;
                if (PromotionType == 2)
                {
                    return $"Giảm {DiscountAmount:N0} ₫ trực tiếp cho ấn phẩm {(string.IsNullOrWhiteSpace(BookTitle) ? $"#{BookId}" : BookTitle)}";
                }
                return $"Giảm {DiscountAmount:N0} ₫ cho đơn hàng từ {(MinOrderValue ?? 0):N0} ₫";
            }
        }
    }

    public class CreatePromotionDto
    {
        [Required(ErrorMessage = "Tên chương trình khuyến mãi không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên chương trình tối đa 255 ký tự")]
        [Display(Name = "Tên chương trình khuyến mãi")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả thể lệ tối đa 500 ký tự")]
        [Display(Name = "Mô tả thể lệ")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại khuyến mãi")]
        [Display(Name = "Loại khuyến mãi")]
        public int PromotionType { get; set; } = 1; // 1 = Hóa đơn, 2 = Sản phẩm

        [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Ngày kết thúc không được để trống")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);

        [Display(Name = "Kích hoạt hiệu lực")]
        public bool IsActive { get; set; } = true;

        // Loại 1: OrderDiscount
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn tối thiểu phải lớn hơn hoặc bằng 0")]
        [Display(Name = "Giá trị đơn hàng tối thiểu (VNĐ)")]
        public decimal? MinOrderValue { get; set; } = 0;

        // Mức giảm cố định VNĐ (chung cho cả 2 loại)
        [Required(ErrorMessage = "Số tiền giảm giá không được để trống")]
        [Range(1000, double.MaxValue, ErrorMessage = "Số tiền giảm tối thiểu phải từ 1.000 ₫")]
        [Display(Name = "Số tiền giảm giá cố định (VNĐ)")]
        public decimal DiscountAmount { get; set; } = 10000;

        // Loại 2: ProductDiscount
        [Display(Name = "Ấn phẩm sách áp dụng")]
        public int? BookId { get; set; }
    }

    public class UpdatePromotionDto
    {
        [Required(ErrorMessage = "Tên chương trình khuyến mãi không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên chương trình tối đa 255 ký tự")]
        [Display(Name = "Tên chương trình khuyến mãi")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Mô tả thể lệ tối đa 500 ký tự")]
        [Display(Name = "Mô tả thể lệ")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại khuyến mãi")]
        [Display(Name = "Loại khuyến mãi")]
        public int PromotionType { get; set; } = 1;

        [Required(ErrorMessage = "Ngày bắt đầu không được để trống")]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Ngày kết thúc không được để trống")]
        [Display(Name = "Ngày kết thúc")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Kích hoạt hiệu lực")]
        public bool IsActive { get; set; }

        // Loại 1: OrderDiscount
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn tối thiểu phải lớn hơn hoặc bằng 0")]
        [Display(Name = "Giá trị đơn hàng tối thiểu (VNĐ)")]
        public decimal? MinOrderValue { get; set; }

        // Mức giảm cố định VNĐ
        [Required(ErrorMessage = "Số tiền giảm giá không được để trống")]
        [Range(1000, double.MaxValue, ErrorMessage = "Số tiền giảm tối thiểu phải từ 1.000 ₫")]
        [Display(Name = "Số tiền giảm giá cố định (VNĐ)")]
        public decimal DiscountAmount { get; set; }

        // Loại 2: ProductDiscount
        [Display(Name = "Ấn phẩm sách áp dụng")]
        public int? BookId { get; set; }
    }
}
