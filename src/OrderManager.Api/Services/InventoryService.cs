using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryService
{
    private readonly HttpClient _httpClient;

    public InventoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItem>> GetAllInventoryAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<InventoryItem>>("api/inventory") ?? new();
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InventoryItem>();
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItem>()
            ?? throw new InvalidOperationException("Failed to deserialize restock response");
    }

    public async Task<InventoryItem> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { Quantity = quantity });
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to deduct stock: {error}");
        }
        return await response.Content.ReadFromJsonAsync<InventoryItem>()
            ?? throw new InvalidOperationException("Failed to deserialize deduct response");
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<InventoryItem>>("api/inventory/low-stock") ?? new();
    }
}
