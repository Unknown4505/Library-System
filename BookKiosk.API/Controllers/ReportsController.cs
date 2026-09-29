using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenueByMonth([FromQuery] int year)
    {
        return Ok(await _reportService.GetRevenueByMonthAsync(year));
    }

    [HttpGet("top-books")]
    public async Task<IActionResult> GetTopSellingBooks([FromQuery] int top = 10)
    {
        return Ok(await _reportService.GetTopSellingBooksAsync(top));
    }
}
