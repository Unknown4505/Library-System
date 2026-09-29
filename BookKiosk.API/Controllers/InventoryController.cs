using BookKiosk.Domain.Entities;
using BookKiosk.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportStock([FromBody] ImportReceipt receipt)
    {
        return Ok(await _inventoryService.ImportStockAsync(receipt));
    }
}
