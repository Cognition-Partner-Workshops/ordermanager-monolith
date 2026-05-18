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
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = "Inventory service unavailable", details = ex.Message });
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
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = "Inventory service unavailable", details = ex.Message });
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
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = "Inventory service unavailable", details = ex.Message });
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        try
        {
            return Ok(await _inventoryClient.GetLowStockItemsAsync());
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { error = "Inventory service unavailable", details = ex.Message });
        }
    }
}

public record RestockRequest(int Quantity);
