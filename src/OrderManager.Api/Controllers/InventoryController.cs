using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Services;

namespace OrderManager.Api.Controllers;

// Thin proxy over the inventory-service so the existing Angular UI keeps working at /api/inventory.
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryClient _inventory;

    public InventoryController(IInventoryClient inventory)
    {
        _inventory = inventory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _inventory.GetAllAsync());

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var item = await _inventory.GetByProductIdAsync(productId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        var item = await _inventory.RestockAsync(productId, request.Quantity);
        return Ok(item);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock() => Ok(await _inventory.GetLowStockAsync());
}

public record RestockRequest(int Quantity);
