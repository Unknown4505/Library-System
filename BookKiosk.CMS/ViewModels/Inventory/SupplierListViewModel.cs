using BookKiosk.CMS.Models.Inventory;

namespace BookKiosk.CMS.ViewModels.Inventory
{
    public class SupplierListViewModel
    {
        public IEnumerable<SupplierDto> Suppliers { get; set; } = new List<SupplierDto>();
        public string? SearchKeyword { get; set; }
        public bool ShowInactive { get; set; }
    }
}
