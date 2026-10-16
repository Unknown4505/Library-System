using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;

namespace BookKiosk.CMS.ViewModels.Inventory
{
    public class ReceiptListViewModel
    {
        public PaginatedResult<ImportReceiptDto> Receipts { get; set; } = new();

        // Filters
        public int? SupplierId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Reference data
        public IEnumerable<SupplierDto> Suppliers { get; set; } = new List<SupplierDto>();
    }
}
