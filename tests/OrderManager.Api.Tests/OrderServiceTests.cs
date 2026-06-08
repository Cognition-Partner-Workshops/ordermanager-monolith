using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
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

    private static InventoryClient CreateInventoryClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5001") };
        return new InventoryClient(httpClient);
    }

    private static InventoryClient CreateStockAvailableClient()
    {
        return CreateInventoryClient(request =>
        {
            var uri = request.RequestUri!.PathAndQuery;

            if (uri.Contains("/check"))
            {
                var json = JsonSerializer.Serialize(new { productId = 1, quantity = 1, available = true });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (uri.Contains("/deduct"))
            {
                var item = new InventoryItem { Id = 1, ProductId = 1, ProductName = "Widget A", Sku = "WGT-001", QuantityOnHand = 45 };
                var json = JsonSerializer.Serialize(item);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });
    }

    private static InventoryClient CreateStockUnavailableClient()
    {
        return CreateInventoryClient(request =>
        {
            var uri = request.RequestUri!.PathAndQuery;

            if (uri.Contains("/check"))
            {
                var json = JsonSerializer.Serialize(new { productId = 1, quantity = 99999, available = false });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            };
        });
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateStockAvailableClient();
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_SucceedsWhenStockAvailable()
    {
        using var context = CreateContext();
        var inventoryClient = CreateStockAvailableClient();
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateStockUnavailableClient();
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
