using System.Collections.Generic;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Inventory;

namespace BookKiosk.CMS.ViewModels.Dashboard
{
    /// <summary>
    /// ViewModel cho Bàn làm việc nhân viên (Staff Workspace).
    /// BẢO MẬT: TUYỆT ĐỐI KHÔNG chứa trường Doanh thu / Tiền tệ tài chính.
    /// </summary>
    public class StaffWorkspaceViewModel
    {
        public List<StockItemDto> LowStockItems { get; set; } = new();
        public List<OrderListItemDto> NeedsSupportOrders { get; set; } = new();

        public int LowStockCount => LowStockItems.Count;
        public int NeedsSupportCount => NeedsSupportOrders.Count;

        public bool IsLowStockUnavailable { get; set; }
        public bool IsOrdersUnavailable { get; set; }
        public bool UseMockApi { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
