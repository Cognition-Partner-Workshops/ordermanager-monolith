using InventoryService.Api.Models;

namespace InventoryService.Api.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryItem>> GetAllInventoryAsync();
    Task<InventoryItem?> GetInventoryByProductIdAsync(int productId);
    Task<InventoryItem> RestockAsync(int productId, int quantity);
    Task<List<InventoryItem>> GetLowStockItemsAsync();
}
