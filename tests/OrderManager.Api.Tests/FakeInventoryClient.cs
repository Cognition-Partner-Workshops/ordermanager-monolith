using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

// In-memory stand-in for the inventory-service, mirroring its HTTP semantics.
public class FakeInventoryClient : IInventoryClient
{
    public Dictionary<int, InventoryItemDto> Items { get; } = new();
    public List<string> Calls { get; } = new();
    public bool FailDeductForProduct { get; set; }

    public FakeInventoryClient(params (int ProductId, int Quantity)[] stock)
    {
        foreach (var (productId, quantity) in stock)
            Items[productId] = new InventoryItemDto { Id = productId, ProductId = productId, ProductName = $"Product {productId}", QuantityOnHand = quantity, ReorderLevel = 10 };
    }

    public Task<List<InventoryItemDto>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.Values.ToList());

    public Task<InventoryItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default) =>
        Task.FromResult(Items.GetValueOrDefault(productId));

    public Task<List<InventoryItemDto>> GetLowStockAsync(CancellationToken ct = default) =>
        Task.FromResult(Items.Values.Where(i => i.QuantityOnHand <= i.ReorderLevel).ToList());

    public Task<InventoryItemDto> RestockAsync(int productId, int quantity, CancellationToken ct = default)
    {
        Calls.Add($"restock:{productId}:{quantity}");
        var item = Items.GetValueOrDefault(productId) ?? throw new ArgumentException($"No inventory record for product {productId}");
        item.QuantityOnHand += quantity;
        return Task.FromResult(item);
    }

    public Task<InventoryItemDto> DeductAsync(int productId, int quantity, CancellationToken ct = default)
    {
        Calls.Add($"deduct:{productId}:{quantity}");
        if (FailDeductForProduct) throw new InventoryServiceUnavailableException("boom");
        var item = Items.GetValueOrDefault(productId) ?? throw new ArgumentException($"No inventory record for product {productId}");
        if (item.QuantityOnHand < quantity) throw new InvalidOperationException($"Insufficient stock for product {productId}");
        item.QuantityOnHand -= quantity;
        return Task.FromResult(item);
    }

    public Task<StockCheckResponse> CheckAvailabilityAsync(IEnumerable<StockCheckLine> lines, CancellationToken ct = default)
    {
        Calls.Add("check");
        var results = lines.Select(l =>
        {
            var available = Items.GetValueOrDefault(l.ProductId)?.QuantityOnHand ?? 0;
            return new StockCheckResult(l.ProductId, l.Quantity, available, available >= l.Quantity);
        }).ToList();
        return Task.FromResult(new StockCheckResponse(results.All(r => r.Sufficient), results));
    }
}
