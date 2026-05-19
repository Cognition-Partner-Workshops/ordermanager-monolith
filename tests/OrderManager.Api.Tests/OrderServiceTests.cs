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

    private static InventoryService CreateInventoryService(HttpClient httpClient)
    {
        return new InventoryService(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var handler = new FakeInventoryHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var inventoryService = CreateInventoryService(httpClient);
        var service = new OrderService(context, inventoryService);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var handler = new FakeInventoryHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var inventoryService = CreateInventoryService(httpClient);
        var service = new OrderService(context, inventoryService);

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.True(handler.DeductCalled, "Expected the inventory deduct endpoint to be called");
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var handler = new FakeInventoryHandler { FailDeduct = true };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var inventoryService = CreateInventoryService(httpClient);
        var service = new OrderService(context, inventoryService);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

internal class FakeInventoryHandler : HttpMessageHandler
{
    public bool DeductCalled { get; private set; }
    public bool FailDeduct { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath.Contains("/deduct") == true)
        {
            DeductCalled = true;
            if (FailDeduct)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new { error = "Insufficient stock" })
                });
            }
            var item = new InventoryItem { Id = 1, ProductId = 1, QuantityOnHand = 45, ReorderLevel = 10, WarehouseLocation = "A-01" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(item)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new List<InventoryItem>())
        });
    }
}
