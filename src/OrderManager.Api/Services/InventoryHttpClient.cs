using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryHttpClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItem>> GetAllInventoryAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory");
        return items?.Select(MapToInventoryItem).ToList() ?? new List<InventoryItem>();
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        return dto is null ? null : MapToInventoryItem(dto);
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", new { quantity });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<InventoryItemDto>()
            ?? throw new InvalidOperationException("Invalid response from inventory service");
        return MapToInventoryItem(dto);
    }

    public async Task<InventoryItem> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { quantity });
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException(error?.Error ?? $"Insufficient stock for product {productId}");
        }
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<InventoryItemDto>()
            ?? throw new InvalidOperationException("Invalid response from inventory service");
        return MapToInventoryItem(dto);
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory/low-stock");
        return items?.Select(MapToInventoryItem).ToList() ?? new List<InventoryItem>();
    }

    private static InventoryItem MapToInventoryItem(InventoryItemDto dto)
    {
        return new InventoryItem
        {
            Id = dto.Id,
            ProductId = dto.ProductId,
            QuantityOnHand = dto.QuantityOnHand,
            ReorderLevel = dto.ReorderLevel,
            WarehouseLocation = dto.WarehouseLocation,
            LastRestocked = dto.LastRestocked
        };
    }

    private record InventoryItemDto(
        int Id,
        int ProductId,
        string ProductName,
        int QuantityOnHand,
        int ReorderLevel,
        string WarehouseLocation,
        DateTime LastRestocked
    );

    private record ErrorResponse(string Error);
}
