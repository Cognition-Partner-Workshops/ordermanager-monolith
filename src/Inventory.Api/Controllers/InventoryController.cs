using Microsoft.AspNetCore.Mvc;
using Inventory.Api.Services;

namespace Inventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _inventoryService.GetAllInventoryAsync());

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var item = await _inventoryService.GetInventoryByProductIdAsync(productId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        var item = await _inventoryService.RestockAsync(productId, request.Quantity);
        return Ok(item);
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock() => Ok(await _inventoryService.GetLowStockItemsAsync());

    [HttpPost("product/{productId}/deduct")]
    public async Task<IActionResult> DeductStock(int productId, [FromBody] DeductRequest request)
    {
        var item = await _inventoryService.DeductStockAsync(productId, request.Quantity);
        if (item is null)
            return BadRequest(new { error = $"Insufficient stock or no inventory record for product {productId}" });
        return Ok(item);
    }
}

public record RestockRequest(int Quantity);
public record DeductRequest(int Quantity);
