using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    private static InventoryHttpClient CreateInventoryClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5001") };
        return new InventoryHttpClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<InventoryItem>()) });
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryDeduct()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventoryItem = await context.InventoryItems.FirstAsync(i => i.ProductId == product.Id);

        var deductCalled = false;
        var inventoryClient = CreateInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains($"/api/inventory/product/{product.Id}/deduct"))
            {
                deductCalled = true;
                var responseItem = new InventoryItem
                {
                    Id = inventoryItem.Id,
                    ProductId = product.Id,
                    QuantityOnHand = inventoryItem.QuantityOnHand - 5,
                    ReorderLevel = inventoryItem.ReorderLevel
                };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(responseItem) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OrderService(context, inventoryClient);
        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.True(deductCalled, "Inventory deduct endpoint should be called");
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var inventoryClient = CreateInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/deduct"))
            {
                var errorJson = JsonSerializer.Serialize(new { error = "Insufficient stock" });
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(errorJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OrderService(context, inventoryClient);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

internal class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
