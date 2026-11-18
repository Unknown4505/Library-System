using BookKiosk.CMS.Models.Inventory;

namespace BookKiosk.CMS.ViewModels.Inventory
{
    public class SupplierListViewModel
    {
        public IEnumerable<SupplierDto> Suppliers { get; set; } = new List<SupplierDto>();
        public string? SearchKeyword { get; set; }
        public bool ShowInactive { get; set; }
        public CreateSupplierDto CreateForm { get; set; } = new();
        public UpdateSupplierDto EditForm { get; set; } = new();
        public int? EditId { get; set; }
        public string? ActiveForm { get; set; }
        public string? LoadError { get; set; }
    }
}
