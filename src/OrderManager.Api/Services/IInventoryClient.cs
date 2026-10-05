using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public interface IInventoryClient
{
    Task<List<InventoryItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<InventoryItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<List<InventoryItemDto>> GetLowStockAsync(CancellationToken ct = default);
    Task<InventoryItemDto> RestockAsync(int productId, int quantity, CancellationToken ct = default);
    Task<InventoryItemDto> DeductAsync(int productId, int quantity, CancellationToken ct = default);
    Task<StockCheckResponse> CheckAvailabilityAsync(IEnumerable<StockCheckLine> items, CancellationToken ct = default);
}

public class InventoryServiceUnavailableException : Exception
{
    public InventoryServiceUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
