using OrderManager.Api.HttpClients;

namespace OrderManager.Api.Services;

public class InventoryService
{
    private readonly InventoryApiClient _inventoryApiClient;

    public InventoryService(InventoryApiClient inventoryApiClient)
    {
        _inventoryApiClient = inventoryApiClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        return await _inventoryApiClient.GetAllInventoryAsync();
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        return await _inventoryApiClient.GetInventoryByProductIdAsync(productId);
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        return await _inventoryApiClient.RestockAsync(productId, quantity);
    }

    public async Task<InventoryItemDto> DeductAsync(int productId, int quantity)
    {
        return await _inventoryApiClient.DeductAsync(productId, quantity);
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        return await _inventoryApiClient.GetLowStockItemsAsync();
    }
}
