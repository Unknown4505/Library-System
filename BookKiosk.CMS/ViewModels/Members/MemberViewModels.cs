using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BookKiosk.CMS.Models.Members;

namespace BookKiosk.CMS.ViewModels.Members
{
    public class MemberIndexViewModel
    {
        [Display(Name = "Số điện thoại thành viên")]
        public string? PhoneNumber { get; set; }

        public MemberDto? Member { get; set; }
        public IEnumerable<PointTransactionDto> RecentHistory { get; set; } = new List<PointTransactionDto>();

        public bool IsSearched { get; set; }
        public bool IsNotFound { get; set; }
        public string? Message { get; set; }
    }

    public class MemberFormViewModel
    {
        public int MemberId { get; set; }

        [Required(ErrorMessage = "Số điện thoại thành viên không được để trống")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại không đúng định dạng di động Việt Nam (10 số, bắt đầu bằng 03, 05, 07, 08, 09)")]
        [Display(Name = "Số điện thoại thành viên")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên thành viên không được để trống")]
        [MaxLength(255, ErrorMessage = "Họ và tên tối đa 255 ký tự")]
        [Display(Name = "Họ và tên thành viên")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Điểm tích lũy hiện tại")]
        public int Points { get; set; }

        public bool IsEditMode => MemberId > 0;
    }

    public class MemberDetailsViewModel
    {
        public MemberDto Member { get; set; } = new();
        public IEnumerable<PointTransactionDto> PointHistory { get; set; } = new List<PointTransactionDto>();
        public string FilterType { get; set; } = "all"; // all, earned, redeemed

        public int TotalEarnedPoints { get; set; }
        public int TotalRedeemedPoints { get; set; }
    }
}
