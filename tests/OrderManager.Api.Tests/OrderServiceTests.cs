using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderManager.Api.Data;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

public class OrderServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedData.Initialize(context);
        return context;
    }

    private static OrderService CreateService(AppDbContext context, FakeInventoryClient inventory) =>
        new(context, inventory, NullLogger<OrderService>.Instance);

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var service = CreateService(context, new FakeInventoryClient());
        Assert.Empty(await service.GetAllOrdersAsync());
    }

    [Fact]
    public async Task CreateOrder_DeductsInventoryViaInventoryClient()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventory = new FakeInventoryClient((product.Id, 50));
        var service = CreateService(context, inventory);

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.Equal(45, inventory.Items[product.Id].QuantityOnHand);
        Assert.Equal(new[] { "check", $"deduct:{product.Id}:5" }, inventory.Calls);
        Assert.Equal(product.Price * 5, order.TotalAmount);
        Assert.Equal(1, await context.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock_WithoutDeducting()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventory = new FakeInventoryClient((product.Id, 3));
        var service = CreateService(context, inventory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));

        Assert.Contains(product.Name, ex.Message);
        Assert.Contains("Available: 3", ex.Message);
        Assert.Equal(3, inventory.Items[product.Id].QuantityOnHand);
        Assert.DoesNotContain(inventory.Calls, c => c.StartsWith("deduct"));
        Assert.Equal(0, await context.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_RestoresDeductedStock_WhenLaterDeductionFails()
    {
        using var context = CreateContext();
        var products = await context.Products.Take(2).ToListAsync();
        var customer = await context.Customers.FirstAsync();
        var inventory = new FakeInventoryClient((products[0].Id, 50), (products[1].Id, 50));
        var service = CreateService(context, inventory);
        var items = new List<(int, int)> { (products[0].Id, 5), (products[1].Id, 7) };

        // First deduction succeeds, then the inventory-service goes away.
        inventory.Items[products[1].Id].QuantityOnHand = 50;
        var failing = new FailAfterFirstDeductClient(inventory);
        service = new OrderService(context, failing, NullLogger<OrderService>.Instance);

        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(() => service.CreateOrderAsync(customer.Id, items));

        Assert.Equal(50, inventory.Items[products[0].Id].QuantityOnHand);
        Assert.Contains($"restock:{products[0].Id}:5", inventory.Calls);
        Assert.Equal(0, await context.Orders.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenInventoryServiceUnavailable()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventory = new FakeInventoryClient((product.Id, 50)) { FailDeductForProduct = true };
        var service = CreateService(context, inventory);

        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 1) }));
        Assert.Equal(0, await context.Orders.CountAsync());
    }

    private sealed class FailAfterFirstDeductClient : IInventoryClient
    {
        private readonly FakeInventoryClient _inner;
        private int _deducts;
        public FailAfterFirstDeductClient(FakeInventoryClient inner) => _inner = inner;

        public Task<List<Api.Models.InventoryItemDto>> GetAllAsync(CancellationToken ct = default) => _inner.GetAllAsync(ct);
        public Task<Api.Models.InventoryItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default) => _inner.GetByProductIdAsync(productId, ct);
        public Task<List<Api.Models.InventoryItemDto>> GetLowStockAsync(CancellationToken ct = default) => _inner.GetLowStockAsync(ct);
        public Task<Api.Models.InventoryItemDto> RestockAsync(int productId, int quantity, CancellationToken ct = default) => _inner.RestockAsync(productId, quantity, ct);
        public Task<Api.Models.StockCheckResponse> CheckAvailabilityAsync(IEnumerable<Api.Models.StockCheckLine> items, CancellationToken ct = default) => _inner.CheckAvailabilityAsync(items, ct);
        public Task<Api.Models.InventoryItemDto> DeductAsync(int productId, int quantity, CancellationToken ct = default)
        {
            if (++_deducts > 1) throw new InventoryServiceUnavailableException("inventory-service down");
            return _inner.DeductAsync(productId, quantity, ct);
        }
    }
}
