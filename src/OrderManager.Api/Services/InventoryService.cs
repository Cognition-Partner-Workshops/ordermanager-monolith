using OrderManager.Api.HttpClients;

namespace OrderManager.Api.Services;

public class InventoryService
{
    private readonly InventoryHttpClient _httpClient;

    public InventoryService(InventoryHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        return await _httpClient.GetAllInventoryAsync();
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        return await _httpClient.GetInventoryByProductIdAsync(productId);
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        return await _httpClient.RestockAsync(productId, quantity);
    }

    public async Task<InventoryItemDto> DeductStockAsync(int productId, int quantity)
    {
        return await _httpClient.DeductStockAsync(productId, quantity);
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        return await _httpClient.GetLowStockItemsAsync();
    }
}
