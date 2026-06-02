using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

/// <summary>
/// HTTP client that proxies inventory operations to the standalone inventory-service microservice.
/// Replaces the in-process InventoryService that previously accessed the local database directly.
/// </summary>
public class InventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItem>> GetAllInventoryAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InventoryItem>>("api/inventory");
        return items ?? new List<InventoryItem>();
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InventoryItem>();
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/restock",
            new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryItem>())!;
    }

    public async Task<InventoryItem> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/deduct",
            new { Quantity = quantity });

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException(error?.Error ?? "Insufficient stock");
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryItem>())!;
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InventoryItem>>("api/inventory/low-stock");
        return items ?? new List<InventoryItem>();
    }

    private record ErrorResponse(string Error);
}
