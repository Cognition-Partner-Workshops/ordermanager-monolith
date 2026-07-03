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

public class FakeProductClient : IProductClient
{
    private readonly Dictionary<int, ProductDto> _products = new();
    private int _nextId;

    public FakeProductClient(IEnumerable<ProductDto> products)
    {
        foreach (var product in products) _products[product.Id] = product;
        _nextId = _products.Count == 0 ? 1 : _products.Keys.Max() + 1;
    }

    public Task<List<ProductDto>> GetAllProductsAsync() =>
        Task.FromResult(_products.Values.ToList());

    public Task<ProductDto?> GetProductByIdAsync(int id) =>
        Task.FromResult(_products.TryGetValue(id, out var product) ? product : null);

    public Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids) =>
        Task.FromResult(ids.Distinct().Where(_products.ContainsKey).Select(id => _products[id]).ToList());

    public Task<List<ProductDto>> GetProductsByCategoryAsync(string category) =>
        Task.FromResult(_products.Values.Where(p => p.Category == category).ToList());

    public Task<ProductDto> CreateProductAsync(ProductDto product)
    {
        if (_products.Values.Any(p => p.Sku == product.Sku))
            throw new InvalidOperationException($"A product with SKU {product.Sku} already exists.");
        product.Id = _nextId++;
        _products[product.Id] = product;
        return Task.FromResult(product);
    }
}

public class OrderServiceTests
{
    private static readonly ProductDto[] Products =
    {
        new ProductDto { Id = 1, Name = "Widget A", Description = "Standard widget", Category = "Widgets", Price = 9.99m, Sku = "WGT-001" },
        new ProductDto { Id = 2, Name = "Widget B", Description = "Premium widget", Category = "Widgets", Price = 19.99m, Sku = "WGT-002" },
        new ProductDto { Id = 3, Name = "Gadget X", Description = "Basic gadget", Category = "Gadgets", Price = 29.99m, Sku = "GDG-001" },
        new ProductDto { Id = 4, Name = "Gadget Y", Description = "Advanced gadget", Category = "Gadgets", Price = 49.99m, Sku = "GDG-002" },
        new ProductDto { Id = 5, Name = "Thingamajig", Description = "Multi-purpose thingamajig", Category = "Misc", Price = 14.99m, Sku = "THG-001" },
    };

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedData.Initialize(context);
        return context;
    }

    private static FakeProductClient CreateProductClient() => new(Products);

    private static FakeInventoryClient CreateInventoryClient()
    {
        var items = Products.Select((p, i) => new InventoryItemDto
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
        var service = new OrderService(context, CreateInventoryClient(), CreateProductClient());
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_DeductsInventory()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient();
        var service = new OrderService(context, inventoryClient, CreateProductClient());
        var productId = Products[0].Id;
        var customer = await context.Customers.FirstAsync();
        var inventoryBefore = await inventoryClient.GetInventoryByProductIdAsync(productId);
        var qtyBefore = inventoryBefore!.QuantityOnHand;

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (productId, 5) });

        var inventoryAfter = await inventoryClient.GetInventoryByProductIdAsync(productId);
        Assert.Equal(qtyBefore - 5, inventoryAfter!.QuantityOnHand);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(), CreateProductClient());
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (Products[0].Id, 99999) }));
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenProductMissing()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(), CreateProductClient());
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (999, 1) }));
    }

    [Fact]
    public async Task CreateOrder_UsesProductPriceAsUnitPriceSnapshot()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(), CreateProductClient());
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (Products[0].Id, 2) });

        var item = Assert.Single(order.Items);
        Assert.Equal(Products[0].Price, item.UnitPrice);
        Assert.Equal(Products[0].Price * 2, order.TotalAmount);
    }
}
