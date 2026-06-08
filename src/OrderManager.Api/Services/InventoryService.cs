using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

/// <summary>
/// Proxy service that delegates inventory operations to the standalone inventory microservice via HTTP.
/// Replaces the previous in-process EF Core implementation.
/// </summary>
public class InventoryService
{
    private readonly InventoryServiceClient _client;

    public InventoryService(InventoryServiceClient client)
    {
        _client = client;
    }

    public async Task<List<InventoryItem>> GetAllInventoryAsync()
    {
        return await _client.GetAllInventoryAsync();
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        return await _client.GetInventoryByProductIdAsync(productId);
    }

    public async Task<InventoryItem> RestockAsync(int productId, int quantity)
    {
        return await _client.RestockAsync(productId, quantity);
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        return await _client.GetLowStockItemsAsync();
    }
}
