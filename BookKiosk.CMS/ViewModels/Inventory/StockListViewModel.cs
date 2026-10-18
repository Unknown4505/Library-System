using BookKiosk.CMS.Models.Inventory;

namespace BookKiosk.CMS.ViewModels.Inventory
{
    public class StockListViewModel
    {
        public IEnumerable<StockItemDto> StockItems { get; set; } = new List<StockItemDto>();

        // KPI metrics
        public int TotalItems => StockItems?.Count() ?? 0;
        public int TotalStockQuantity => StockItems?.Sum(s => s.StockQuantity) ?? 0;
        public int LowStockCount => StockItems?.Count(s => s.IsLowStock && s.AvailableStock > 0) ?? 0;
        public int OutOfStockCount => StockItems?.Count(s => s.AvailableStock <= 0) ?? 0;

        // Bộ lọc
        public bool LowOnly { get; set; }
        public string? Keyword { get; set; }

        // Model cho popup điều chỉnh kiểm kê
        public AdjustStockDto AdjustModel { get; set; } = new();
    }
}
