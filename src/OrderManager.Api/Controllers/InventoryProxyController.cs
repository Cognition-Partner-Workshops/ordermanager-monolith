using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.HttpClients;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryProxyController : ControllerBase
{
    private readonly InventoryHttpClient _inventoryClient;

    public InventoryProxyController(InventoryHttpClient inventoryClient)
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
        catch (InventoryServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProductId(int productId)
    {
        try
        {
            var item = await _inventoryClient.GetInventoryByProductIdAsync(productId);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InventoryServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        try
        {
            var result = await _inventoryClient.RestockAsync(productId, request.Quantity);
            return Ok(result);
        }
        catch (InventoryServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("product/{productId}/deduct")]
    public async Task<IActionResult> Deduct(int productId, [FromBody] DeductRequest request)
    {
        try
        {
            var result = await _inventoryClient.DeductStockAsync(productId, request.Quantity);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InventoryServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        try
        {
            return Ok(await _inventoryClient.GetLowStockItemsAsync());
        }
        catch (InventoryServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}

public record RestockRequest(int Quantity);
public record DeductRequest(int Quantity);
