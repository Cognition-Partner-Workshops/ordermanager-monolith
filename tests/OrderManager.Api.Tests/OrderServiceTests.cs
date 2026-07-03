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

public class FakeCustomerClient : ICustomerClient
{
    private readonly Dictionary<int, CustomerDto> _customers = new();

    public FakeCustomerClient(IEnumerable<CustomerDto> customers)
    {
        foreach (var customer in customers) _customers[customer.Id] = customer;
    }

    public Task<List<CustomerDto>> GetAllCustomersAsync() =>
        Task.FromResult(_customers.Values.ToList());

    public Task<CustomerDto?> GetCustomerByIdAsync(int id) =>
        Task.FromResult(_customers.TryGetValue(id, out var customer) ? customer : null);

    public Task<CustomerDto> CreateCustomerAsync(CustomerDto customer)
    {
        customer.Id = _customers.Count == 0 ? 1 : _customers.Keys.Max() + 1;
        _customers[customer.Id] = customer;
        return Task.FromResult(customer);
    }
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

    private static FakeCustomerClient CreateCustomerClient()
    {
        return new FakeCustomerClient(new[]
        {
            new CustomerDto { Id = 1, Name = "Acme Corp", Email = "orders@acme.com", Address = "123 Main St", City = "Springfield", State = "IL", ZipCode = "62701" },
            new CustomerDto { Id = 2, Name = "Globex Inc", Email = "purchasing@globex.com", Address = "456 Oak Ave", City = "Shelbyville", State = "IL", ZipCode = "62565" },
        });
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var service = new OrderService(context, CreateInventoryClient(context), CreateCustomerClient());
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_DeductsInventory()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(context);
        var customerClient = CreateCustomerClient();
        var service = new OrderService(context, inventoryClient, customerClient);
        var product = await context.Products.FirstAsync();
        var customer = (await customerClient.GetAllCustomersAsync()).First();
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
        var customerClient = CreateCustomerClient();
        var service = new OrderService(context, CreateInventoryClient(context), customerClient);
        var product = await context.Products.FirstAsync();
        var customer = (await customerClient.GetAllCustomersAsync()).First();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
