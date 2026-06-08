using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        var response = await _httpClient.GetAsync("/api/inventory");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>() ?? new();
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"/api/inventory/product/{productId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/inventory/product/{productId}/restock", new { quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>()
            ?? throw new InvalidOperationException("Failed to deserialize restock response");
    }

    public async Task<InventoryItemDto> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/inventory/product/{productId}/deduct", new { quantity });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException(error?.Error ?? "Insufficient stock");
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>()
            ?? throw new InvalidOperationException("Failed to deserialize deduct response");
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        var response = await _httpClient.GetAsync("/api/inventory/low-stock");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>() ?? new();
    }
}

public class InventoryItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
