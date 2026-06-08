using OrderManager.Api.Models;

namespace OrderManager.Api.Services.Clients;

public interface IInventoryServiceClient
{
    Task<InventoryItem?> GetInventoryByProductIdAsync(int productId);
    Task<InventoryItem> DeductStockAsync(int productId, int quantity);
}
