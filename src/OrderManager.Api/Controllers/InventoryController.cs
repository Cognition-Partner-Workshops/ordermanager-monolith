using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Services;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryServiceHttpClient _inventoryClient;

    public InventoryController(InventoryServiceHttpClient inventoryClient)
    {
        _inventoryClient = inventoryClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _inventoryClient.GetAllInventoryAsync());

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var item = await _inventoryClient.GetInventoryByProductIdAsync(productId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        try
        {
            var item = await _inventoryClient.RestockAsync(productId, request.Quantity);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock() => Ok(await _inventoryClient.GetLowStockItemsAsync());
}

public record RestockRequest(int Quantity);
