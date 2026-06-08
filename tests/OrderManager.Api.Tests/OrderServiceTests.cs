using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;
using Xunit;

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

    private static InventoryServiceClient CreateInventoryClient(HttpStatusCode statusCode, object? responseBody = null)
    {
        var handler = new FakeHttpHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100") };
        return new InventoryServiceClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(HttpStatusCode.OK, new InventoryItem());
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var deductedItem = new InventoryItem { ProductId = product.Id, ProductName = product.Name, QuantityOnHand = 45 };
        var inventoryClient = CreateInventoryClient(HttpStatusCode.OK, deductedItem);
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
        var inventoryClient = CreateInventoryClient(HttpStatusCode.Conflict,
            new { error = "Insufficient stock" });
        var service = new OrderService(context, inventoryClient);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

internal class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly object? _responseBody;

    public FakeHttpHandler(HttpStatusCode statusCode, object? responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode);
        if (_responseBody is not null)
        {
            response.Content = new StringContent(
                JsonSerializer.Serialize(_responseBody),
                System.Text.Encoding.UTF8,
                "application/json");
        }
        return Task.FromResult(response);
    }
}
