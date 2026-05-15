using System.Net;
using System.Net.Http.Json;
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

    private static InventoryServiceClient CreateFakeInventoryClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new FakeHttpMessageHandler(handler);
        var httpClient = new HttpClient(messageHandler)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        return new InventoryServiceClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateFakeInventoryClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<InventoryItem>())
            });
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

        var inventoryClient = CreateFakeInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/deduct"))
            {
                var item = new InventoryItem
                {
                    Id = 1,
                    ProductId = product.Id,
                    QuantityOnHand = 45,
                    ReorderLevel = 10,
                    WarehouseLocation = "A-01",
                    LastRestocked = DateTime.UtcNow
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(item)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

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

        var inventoryClient = CreateFakeInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/deduct"))
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OrderService(context, inventoryClient);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
