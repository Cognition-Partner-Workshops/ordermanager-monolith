using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

/// <summary>
/// A test HTTP message handler that returns configurable responses
/// for simulating the inventory-service HTTP API.
/// </summary>
public class MockInventoryHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();

    public void SetupDeductSuccess(int productId)
    {
        _handlers[$"api/inventory/product/{productId}/deduct"] = _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new InventoryItemDto
                {
                    Id = 1,
                    ProductId = productId,
                    QuantityOnHand = 95,
                    ReorderLevel = 10,
                    WarehouseLocation = "A1"
                })
            };
    }

    public void SetupDeductInsufficientStock(int productId)
    {
        _handlers[$"api/inventory/product/{productId}/deduct"] = _ =>
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new { error = $"Insufficient stock for product {productId}" })
            };
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery.TrimStart('/') ?? "";
        if (_handlers.TryGetValue(path, out var handler))
            return Task.FromResult(handler(request));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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

    private InventoryHttpClient CreateInventoryClient(MockInventoryHttpHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001/") };
        return new InventoryHttpClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var handler = new MockInventoryHttpHandler();
        var inventoryClient = CreateInventoryClient(handler);
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryServiceToDeductStock()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var handler = new MockInventoryHttpHandler();
        handler.SetupDeductSuccess(product.Id);
        var inventoryClient = CreateInventoryClient(handler);
        var service = new OrderService(context, inventoryClient);

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var handler = new MockInventoryHttpHandler();
        handler.SetupDeductInsufficientStock(product.Id);
        var inventoryClient = CreateInventoryClient(handler);
        var service = new OrderService(context, inventoryClient);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
