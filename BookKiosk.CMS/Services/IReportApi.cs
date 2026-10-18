using System.Collections.Generic;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Reports;

namespace BookKiosk.CMS.Services
{
    public interface IReportApi
    {
        Task<ApiResult<RevenueReportResponseDto>> GetRevenueByMonthAsync(int year);
        Task<ApiResult<IEnumerable<TopSellingBookDto>>> GetTopSellingBooksAsync(int top = 10);
    }
}
