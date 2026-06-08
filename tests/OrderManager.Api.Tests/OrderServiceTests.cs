using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;
using Xunit;

namespace OrderManager.Api.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();

    public void SetupGet(string path, object responseBody)
    {
        _handlers[$"GET:{path}"] = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseBody)
        };
    }

    public void SetupPost(string path, object responseBody)
    {
        _handlers[$"POST:{path}"] = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseBody)
        };
    }

    public void SetupPostFailure(string path)
    {
        _handlers[$"POST:{path}"] = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { error = "Insufficient stock" })
        };
    }

    public void SetupGetFailure(string path)
    {
        _handlers[$"GET:{path}"] = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";
        var key = $"{request.Method}:{path}";

        if (_handlers.TryGetValue(key, out var handler))
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

    private static InventoryServiceClient CreateInventoryClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new InventoryServiceClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var handler = new MockHttpMessageHandler();
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

        var handler = new MockHttpMessageHandler();
        handler.SetupGet($"/api/inventory/product/{product.Id}/check?quantity=5",
            new { productId = product.Id, quantity = 5, available = true });
        handler.SetupPost($"/api/inventory/product/{product.Id}/deduct",
            new InventoryItemDto
            {
                Id = 1, ProductId = product.Id, ProductName = product.Name,
                Sku = product.Sku, QuantityOnHand = 45, ReorderLevel = 10,
                WarehouseLocation = "A-01", LastRestocked = DateTime.UtcNow
            });

        var inventoryClient = CreateInventoryClient(handler);
        var service = new OrderService(context, inventoryClient);

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(5, order.Items.First().Quantity);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var handler = new MockHttpMessageHandler();
        handler.SetupGetFailure($"/api/inventory/product/{product.Id}/check?quantity=99999");

        var inventoryClient = CreateInventoryClient(handler);
        var service = new OrderService(context, inventoryClient);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
