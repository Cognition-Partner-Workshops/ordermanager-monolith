using Microsoft.AspNetCore.Mvc;

namespace OrderManager.Api.Controllers;

/// <summary>
/// Proxies inventory requests to the standalone inventory-service microservice.
/// The monolith no longer owns inventory data directly.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public InventoryController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("InventoryService");
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _httpClient.GetAsync("api/inventory");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }

    [HttpPost("product/{productId}/restock")]
    public async Task<IActionResult> Restock(int productId, [FromBody] RestockRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var response = await _httpClient.GetAsync("api/inventory/low-stock");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
    }
}

public record RestockRequest(int Quantity);
