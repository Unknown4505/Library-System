using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BookKiosk.CMS.ViewModels.Inventory
{
    public class CreateReceiptViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [Display(Name = "Nhà cung cấp")]
        public int SupplierId { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự")]
        [Display(Name = "Ghi chú nhập kho")]
        public string? Notes { get; set; }

        public List<SelectListItem> SupplierOptions { get; set; } = new();
    }
}
