using System.Net;
using System.Net.Http.Json;

namespace OrderManager.Api.Clients;

public class InventoryHttpClient : IInventoryClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory")
            ?? new List<InventoryItemDto>();
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", new { Quantity = quantity });
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ArgumentException($"No inventory record for product {productId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryItemDto>())!;
    }

    public async Task<InventoryItemDto> DeductAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { Quantity = quantity });
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException($"No inventory record for product {productId}");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException($"Insufficient stock for product {productId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryItemDto>())!;
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory/low-stock")
            ?? new List<InventoryItemDto>();
    }
}
