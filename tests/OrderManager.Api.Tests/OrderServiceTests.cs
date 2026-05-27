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

    private static InventoryServiceClient CreateInventoryClient(bool deductSuccess = true)
    {
        var handler = new FakeInventoryHandler(deductSuccess);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        return new InventoryServiceClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient();
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(deductSuccess: true);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(deductSuccess: false);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

internal class FakeInventoryHandler : HttpMessageHandler
{
    private readonly bool _stockAvailable;

    public FakeInventoryHandler(bool stockAvailable)
    {
        _stockAvailable = stockAvailable;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        // Handle CheckStock endpoint
        if (path.Contains("/check"))
        {
            var checkResponse = new { available = _stockAvailable };
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = JsonContent.Create(checkResponse);
            return Task.FromResult(response);
        }

        // Handle DeductStock endpoint
        if (path.Contains("/deduct"))
        {
            var response = new HttpResponseMessage(_stockAvailable ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            response.Content = JsonContent.Create(new { message = _stockAvailable ? "Stock deducted" : "Insufficient stock" });
            return Task.FromResult(response);
        }

        // Default fallback
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
