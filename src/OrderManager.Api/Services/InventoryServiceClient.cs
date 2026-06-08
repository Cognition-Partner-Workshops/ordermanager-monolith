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

    public async Task<List<InventoryItem>> GetAllInventoryAsync()
    {
        var response = await _httpClient.GetAsync("api/inventory");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItem>>() ?? new List<InventoryItem>();
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItem>();
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItem>()
            ?? throw new InvalidOperationException("Failed to deserialize restock response");
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        var response = await _httpClient.GetAsync("api/inventory/low-stock");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItem>>() ?? new List<InventoryItem>();
    }

    public async Task<bool> CheckStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}/check?quantity={quantity}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockCheckResult>();
        return result?.Available ?? false;
    }

    public async Task<InventoryItem?> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { Quantity = quantity });
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<InventoryItem>();
    }

    private record StockCheckResult(int ProductId, int Quantity, bool Available);
}
