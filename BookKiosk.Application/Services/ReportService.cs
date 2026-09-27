using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Interfaces.Repositories;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IReportService
{
    Task<ApiResponseDto<object>> GetRevenueByMonthAsync(int year);
    Task<ApiResponseDto<object>> GetTopSellingBooksAsync(int top);
}

public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;

    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<ApiResponseDto<object>> GetRevenueByMonthAsync(int year)
    {
        if (year <= 0) year = System.DateTime.Now.Year;
        var result = await _reportRepository.GetRevenueByMonthAsync(year);
        return ApiResponseDto<object>.Ok(new { Year = year, Data = result });
    }

    public async Task<ApiResponseDto<object>> GetTopSellingBooksAsync(int top)
    {
        var result = await _reportRepository.GetTopSellingBooksAsync(top);
        return ApiResponseDto<object>.Ok(result);
    }
}
