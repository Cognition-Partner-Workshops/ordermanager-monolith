using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryApiClient
{
    private readonly HttpClient _httpClient;

    public InventoryApiClient(HttpClient httpClient)
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
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<InventoryItemDto>($"api/inventory/product/{productId}");
            return dto is null ? null : MapToInventoryItem(dto);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/restock",
            new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        return MapToInventoryItem(dto!);
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory/low-stock");
        return items?.Select(MapToInventoryItem).ToList() ?? new List<InventoryItem>();
    }

    public async Task<int> GetStockQuantityAsync(int productId)
    {
        var item = await GetInventoryByProductIdAsync(productId);
        return item?.QuantityOnHand ?? 0;
    }

    public async Task DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/deduct",
            new { Quantity = quantity });

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException(error?.Error ?? "Insufficient stock");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new InvalidOperationException(error?.Error ?? $"No inventory record for product {productId}");
        }

        response.EnsureSuccessStatusCode();
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
}

public record InventoryItemDto(
    int Id,
    int ProductId,
    string ProductName,
    int QuantityOnHand,
    int ReorderLevel,
    string WarehouseLocation,
    DateTime LastRestocked);

public record ErrorResponse(string Error);
