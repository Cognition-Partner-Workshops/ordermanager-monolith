using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Clients;
using OrderManager.Api.Data;
using OrderManager.Api.Services;
using Xunit;

namespace OrderManager.Api.Tests;

public class FakeInventoryClient : IInventoryClient
{
    private readonly Dictionary<int, InventoryItemDto> _items = new();

    public FakeInventoryClient(IEnumerable<InventoryItemDto> items)
    {
        foreach (var item in items) _items[item.ProductId] = item;
    }

    public Task<List<InventoryItemDto>> GetAllInventoryAsync() =>
        Task.FromResult(_items.Values.ToList());

    public Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId) =>
        Task.FromResult(_items.TryGetValue(productId, out var item) ? item : null);

    public Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        if (!_items.TryGetValue(productId, out var item))
            throw new ArgumentException($"No inventory record for product {productId}");
        item.QuantityOnHand += quantity;
        item.LastRestocked = DateTime.UtcNow;
        return Task.FromResult(item);
    }

    public Task<InventoryItemDto> DeductAsync(int productId, int quantity)
    {
        if (!_items.TryGetValue(productId, out var item))
            throw new InvalidOperationException($"No inventory record for product {productId}");
        if (item.QuantityOnHand < quantity)
            throw new InvalidOperationException($"Insufficient stock for product {productId}");
        item.QuantityOnHand -= quantity;
        return Task.FromResult(item);
    }

    public Task<List<InventoryItemDto>> GetLowStockItemsAsync() =>
        Task.FromResult(_items.Values.Where(i => i.QuantityOnHand <= i.ReorderLevel).ToList());
}

public class OrderServiceTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedData.Initialize(context);
        return context;
    }

    private FakeInventoryClient CreateInventoryClient(AppDbContext context)
    {
        var items = context.Products.AsEnumerable().Select((p, i) => new InventoryItemDto
        {
            Id = i + 1,
            ProductId = p.Id,
            ProductName = p.Name,
            Sku = p.Sku,
            QuantityOnHand = (i + 1) * 50,
            ReorderLevel = 10,
            WarehouseLocation = $"A-{i + 1:D2}"
        });
        return new FakeInventoryClient(items);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(context));
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_DeductsInventory()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(context);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventoryBefore = await inventoryClient.GetInventoryByProductIdAsync(product.Id);
        var qtyBefore = inventoryBefore!.QuantityOnHand;

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        var inventoryAfter = await inventoryClient.GetInventoryByProductIdAsync(product.Id);
        Assert.Equal(qtyBefore - 5, inventoryAfter!.QuantityOnHand);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(context));
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
