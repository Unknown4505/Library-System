using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BookKiosk.CMS.Models.Promotions
{
    public class PromotionOrderDiscountDto
    {
        public decimal MinOrderValue { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class PromotionProductDiscountDto
    {
        public int BookId { get; set; }
        public string? BookTitle { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class PromotionDto
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PromotionType { get; set; } = 1; // 1 = OrderDiscount, 2 = ProductDiscount
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);
        public bool IsActive { get; set; } = true;

        public PromotionOrderDiscountDto? OrderDiscount { get; set; }
        public List<PromotionProductDiscountDto> ProductDiscounts { get; set; } = new();

        // Helper getters phục vụ tương thích hiển thị UI
        public decimal? MinOrderValue => OrderDiscount?.MinOrderValue;
        public decimal DiscountAmount => PromotionType == 2
            ? (ProductDiscounts.Count > 0 ? ProductDiscounts[0].DiscountAmount : 0)
            : (OrderDiscount?.DiscountAmount ?? 0);
        public int? BookId => ProductDiscounts.Count > 0 ? ProductDiscounts[0].BookId : null;
        public string? BookTitle => ProductDiscounts.Count > 0 ? ProductDiscounts[0].BookTitle : null;

        public string TypeName => PromotionType == 2 
            ? "Giảm giá từng cuốn sách (ProductDiscount)" 
            : "Giảm giá trên hóa đơn (OrderDiscount)";

        public bool IsEffectiveNow => IsActive && DateTime.UtcNow >= StartDate && DateTime.UtcNow <= EndDate;

        public string DisplayStatus
        {
            get
            {
                if (!IsActive) return "Đã tạm ngưng";
                if (DateTime.UtcNow < StartDate) return "Sắp diễn ra";
                if (DateTime.UtcNow > EndDate) return "Đã hết hạn";
                return "Đang hiệu lực";
            }
        }

        public string DisplayBadgeClass
        {
            get
            {
                if (!IsActive) return "bg-secondary-subtle text-secondary border border-secondary-subtle";
                if (DateTime.UtcNow < StartDate) return "bg-info-subtle text-info border border-info-subtle";
                if (DateTime.UtcNow > EndDate) return "bg-dark-subtle text-muted border";
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
                    if (ProductDiscounts != null && ProductDiscounts.Count > 0)
                    {
                        var count = ProductDiscounts.Count;
                        var sample = ProductDiscounts[0];
                        var name = !string.IsNullOrWhiteSpace(sample.BookTitle) ? sample.BookTitle : $"#{sample.BookId}";
                        return count == 1
                            ? $"Giảm {sample.DiscountAmount:N0} ₫ cho ấn phẩm {name}"
                            : $"Giảm giá cho {count} ấn phẩm sách trong chương trình";
                    }
                    return "Giảm giá theo từng cuốn sách";
                }
                var minVal = OrderDiscount?.MinOrderValue ?? 0;
                var discount = OrderDiscount?.DiscountAmount ?? 0;
                return $"Giảm {discount:N0} ₫ cho đơn hàng từ {minVal:N0} ₫";
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

        public PromotionOrderDiscountDto? OrderDiscount { get; set; }
        public List<PromotionProductDiscountDto> ProductDiscounts { get; set; } = new();
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

        public PromotionOrderDiscountDto? OrderDiscount { get; set; }
        public List<PromotionProductDiscountDto> ProductDiscounts { get; set; } = new();
    }
}
