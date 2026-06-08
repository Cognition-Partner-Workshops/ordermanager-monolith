using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
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

    private InventoryServiceClient CreateInventoryClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        return new InventoryServiceClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var deductCalled = false;
        var inventoryClient = CreateInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/deduct"))
            {
                deductCalled = true;
                var dto = new InventoryItemDto { ProductId = 1, QuantityOnHand = 45 };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto), System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });
        Assert.True(deductCalled);
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenInventoryServiceReturnsConflict()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("/deduct"))
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { error = "Insufficient stock" }),
                        System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
