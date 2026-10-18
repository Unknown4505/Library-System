using System;
using System.Collections.Generic;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.ViewModels.Orders
{
    public class OrderListViewModel
    {
        public OrderFilterDto Filter { get; set; } = new();
        public PaginatedResult<OrderListItemDto> Orders { get; set; } = new();
        
        public int NeedsSupportCount { get; set; }
        public string ActiveTab { get; set; } = "all"; // "all" | "support" | "kiosk" | "counter"
        
        public bool IsEndpointUnavailable { get; set; }
        public string? EndpointMessage { get; set; }
        public bool IsDevelopment { get; set; }
        public bool UseMockApi { get; set; }
    }

    public enum DetailsState
    {
        Found,
        NotFound,
        EndpointUnavailable
    }

    public class OrderDetailViewModel
    {
        public DetailsState State { get; set; } = DetailsState.NotFound;
        public OrderDetailDto? Order { get; set; }
        
        /// <summary>
        /// Theo xác nhận Tech Lead:
        /// - Đơn Quầy (Counter) đang Pending: Cho phép nhân viên hủy để nhả kho.
        /// - Đơn Kiosk: CMS CHỈ XEM đối với đơn Kiosk / đơn lệch tiền (Backend chưa có route hủy Kiosk cho Staff).
        /// </summary>
        public bool CanCancel => State == DetailsState.Found 
            && Order != null 
            && Order.OrderId > 0 
            && Order.SaleChannel == SaleChannel.Counter 
            && Order.OrderStatus == OrderStatus.Pending;

        public bool IsEndpointUnavailable => State == DetailsState.EndpointUnavailable;
        public string? EndpointMessage { get; set; }
    }
}
