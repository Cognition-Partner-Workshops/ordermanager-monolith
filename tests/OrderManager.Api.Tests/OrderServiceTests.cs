using Microsoft.EntityFrameworkCore;
using Xunit;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

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

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var handler = new MockInventoryHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var inventoryClient = new InventoryServiceClient(httpClient);
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var handler = new MockInventoryHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var inventoryClient = new InventoryServiceClient(httpClient);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.True(handler.DeductCalled, "Inventory service deduct endpoint should have been called");
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var handler = new MockInventoryHandler(insufficientStock: true);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var inventoryClient = new InventoryServiceClient(httpClient);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

/// <summary>
/// Mock HTTP handler to simulate the inventory microservice responses in tests.
/// </summary>
public class MockInventoryHandler : HttpMessageHandler
{
    private readonly bool _insufficientStock;
    public bool DeductCalled { get; private set; }

    public MockInventoryHandler(bool insufficientStock = false)
    {
        _insufficientStock = insufficientStock;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";

        if (path.Contains("/deduct"))
        {
            DeductCalled = true;

            if (_insufficientStock)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Conflict)
                {
                    Content = new StringContent("{\"error\":\"Insufficient stock\"}", System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":1,\"productId\":1,\"productName\":\"Widget A\",\"sku\":\"WGT-001\",\"quantityOnHand\":45,\"reorderLevel\":10,\"warehouseLocation\":\"A-01\"}", System.Text.Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        });
    }
}
