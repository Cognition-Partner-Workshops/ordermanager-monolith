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

    private static InventoryHttpClient CreateMockInventoryClient(HttpStatusCode statusCode = HttpStatusCode.OK, InventoryItemDto? responseDto = null)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseDto);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        return new InventoryHttpClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient();
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
        var responseDto = new InventoryItemDto
        {
            Id = 1, ProductId = product.Id, ProductName = product.Name,
            Sku = "WGT-001", QuantityOnHand = 45, ReorderLevel = 10,
            WarehouseLocation = "A-01", LastRestocked = DateTime.UtcNow
        };
        var inventoryClient = CreateMockInventoryClient(HttpStatusCode.OK, responseDto);
        var service = new OrderService(context, inventoryClient);

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenInventoryServiceReturnsError()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient(HttpStatusCode.Conflict);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly InventoryItemDto? _responseDto;

    public MockHttpMessageHandler(HttpStatusCode statusCode, InventoryItemDto? responseDto)
    {
        _statusCode = statusCode;
        _responseDto = responseDto;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode);
        if (_responseDto is not null)
        {
            response.Content = JsonContent.Create(_responseDto);
        }
        else if (_statusCode != HttpStatusCode.OK)
        {
            response.Content = new StringContent("Insufficient stock");
        }
        return Task.FromResult(response);
    }
}
