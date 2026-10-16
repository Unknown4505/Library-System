using Microsoft.AspNetCore.Html;

namespace BookKiosk.CMS.Helpers
{
    public static class StatusBadgeHelper
    {
        public static IHtmlContent RenderBadge(string statusText, string badgeType = "primary")
        {
            var html = $"<span class=\"badge bg-{badgeType} px-2 py-1\">{statusText}</span>";
            return new HtmlString(html);
        }

        public static IHtmlContent RenderOrderStatus(int status)
        {
            return status switch
            {
                1 => new HtmlString("<span class=\"badge bg-warning text-dark\"><i class=\"fas fa-clock me-1\"></i>Chờ thanh toán</span>"),
                2 => new HtmlString("<span class=\"badge bg-success\"><i class=\"fas fa-check-circle me-1\"></i>Đã thanh toán</span>"),
                3 => new HtmlString("<span class=\"badge bg-danger\"><i class=\"fas fa-times-circle me-1\"></i>Đã hủy</span>"),
                _ => new HtmlString("<span class=\"badge bg-secondary\">Không xác định</span>")
            };
        }

        public static IHtmlContent RenderStockStatus(int availableStock)
        {
            if (availableStock <= 0)
            {
                return new HtmlString("<span class=\"badge bg-danger\"><i class=\"fas fa-exclamation-triangle me-1\"></i>Hết hàng</span>");
            }
            if (availableStock <= 5)
            {
                return new HtmlString($"<span class=\"badge bg-warning text-dark\"><i class=\"fas fa-bell me-1\"></i>Sắp hết ({availableStock})</span>");
            }
            return new HtmlString($"<span class=\"badge bg-success\"><i class=\"fas fa-boxes me-1\"></i>Còn hàng ({availableStock})</span>");
        }
    }
}
