using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Services;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryHttpClient _inventoryClient;

    public InventoryController(InventoryHttpClient inventoryClient)
    {
        _inventoryClient = inventoryClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            return Ok(await _inventoryClient.GetAllInventoryAsync());
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Inventory service unavailable" });
        }
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        try
        {
            var item = await _inventoryClient.GetInventoryByProductIdAsync(productId);
            return item is null ? NotFound() : Ok(item);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Inventory service unavailable" });
        }
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        try
        {
            var item = await _inventoryClient.RestockAsync(productId, request.Quantity);
            return Ok(item);
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Inventory service unavailable" });
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        try
        {
            return Ok(await _inventoryClient.GetLowStockItemsAsync());
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { error = "Inventory service unavailable" });
        }
    }
}

public record RestockRequest(int Quantity);
