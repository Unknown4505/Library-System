using System.Collections.Generic;
using BookKiosk.CMS.Models.Api.Pos;

namespace BookKiosk.CMS.ViewModels.Pos
{
    public class PosIndexViewModel
    {
        public bool IsDevelopment { get; set; }
        public bool UseMockApi { get; set; }
        public int OrderTimeoutMinutes { get; set; } = 4;
        public bool ShowMemberNameOnReceipt { get; set; } = false;
        public string CashierName { get; set; } = "Thu ngân";

        public IEnumerable<PosCategoryDto> Categories { get; set; } = new List<PosCategoryDto>();
        public IEnumerable<PosPromotionDto> ActivePromotions { get; set; } = new List<PosPromotionDto>();
    }
}
