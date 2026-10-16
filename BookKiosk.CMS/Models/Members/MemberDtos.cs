using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookKiosk.CMS.Models.Members
{
    public class MemberDto
    {
        public int MemberId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Points { get; set; }

        // Quy đổi điểm tích lũy: 1 điểm = 1.000 ₫
        public decimal PointMoneyValue => Points * 1000m;
        public string FormattedPoints => Points.ToString("N0") + " điểm";
        public string FormattedPointMoneyValue => PointMoneyValue.ToString("N0") + " ₫";

        // Xếp hạng thành viên theo mức điểm
        public string RankName
        {
            get
            {
                if (Points >= 1000) return "Kim Cương (Diamond)";
                if (Points >= 500) return "Bạch Kim (Platinum)";
                if (Points >= 200) return "Vàng (Gold)";
                if (Points >= 50) return "Bạc (Silver)";
                return "Chuẩn (Standard)";
            }
        }

        public string RankBadgeClass
        {
            get
            {
                if (Points >= 1000) return "bg-primary text-white";
                if (Points >= 500) return "bg-info-subtle text-info border border-info-subtle";
                if (Points >= 200) return "bg-warning-subtle text-warning border border-warning-subtle";
                if (Points >= 50) return "bg-secondary-subtle text-secondary border border-secondary-subtle";
                return "bg-light text-dark border";
            }
        }
    }

    public class CreateMemberDto
    {
        [Required(ErrorMessage = "Số điện thoại thành viên không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng di động Việt Nam (10 số, bắt đầu bằng 03, 05, 07, 08, 09)")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên thành viên không được để trống")]
        [MaxLength(255, ErrorMessage = "Họ và tên tối đa 255 ký tự")]
        [Display(Name = "Họ và tên thành viên")]
        public string FullName { get; set; } = string.Empty;
    }

    public class UpdateMemberDto
    {
        public int MemberId { get; set; }

        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Họ và tên thành viên không được để trống")]
        [MaxLength(255, ErrorMessage = "Họ và tên tối đa 255 ký tự")]
        [Display(Name = "Họ và tên thành viên")]
        public string FullName { get; set; } = string.Empty;
    }

    public class PointTransactionDto
    {
        public int PointTransactionId { get; set; }
        public int MemberId { get; set; }
        public int? OrderId { get; set; }
        public int Type { get; set; } // 1 = Tích điểm (Earned), 2 = Tiêu điểm (Redeemed)
        public int Points { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Helpers phục vụ UI
        public string TypeText => Type == 1 ? "Tích điểm (+)" : "Tiêu điểm (-)";
        public string PointsDisplay => Type == 1 ? $"+{Points:N0}" : $"-{Points:N0}";
        public string BadgeClass => Type == 1 
            ? "bg-success-subtle text-success border border-success-subtle" 
            : "bg-danger-subtle text-danger border border-danger-subtle";
        public string MoneyEquivalent => (Points * 1000m).ToString("N0") + " ₫";
    }

    public class MemberDetailViewModel
    {
        public MemberDto Member { get; set; } = new();
        public IEnumerable<PointTransactionDto> PointHistory { get; set; } = new List<PointTransactionDto>();
    }
}
