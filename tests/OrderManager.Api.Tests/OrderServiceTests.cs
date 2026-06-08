using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    private static InventoryServiceHttpClient CreateMockInventoryClient(
        Func<HttpRequestMessage, HttpResponseMessage>? handler = null)
    {
        var mockHandler = new MockHttpMessageHandler(handler ?? DefaultDeductHandler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        var logger = new LoggerFactory().CreateLogger<InventoryServiceHttpClient>();
        return new InventoryServiceHttpClient(httpClient, logger);
    }

    private static HttpResponseMessage DefaultDeductHandler(HttpRequestMessage request)
    {
        if (request.RequestUri?.PathAndQuery.Contains("/deduct") == true)
        {
            var dto = new InventoryItemDto
            {
                Id = 1,
                ProductId = 1,
                ProductName = "Test Product",
                QuantityOnHand = 95,
                ReorderLevel = 10,
                WarehouseLocation = "A1",
                LastRestocked = DateTime.UtcNow
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto)
            };
        }
        return new HttpResponseMessage(HttpStatusCode.OK);
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
    public async Task CreateOrder_CallsInventoryServiceToDeductStock()
    {
        using var context = CreateContext();
        var deductCalled = false;
        var inventoryClient = CreateMockInventoryClient(request =>
        {
            if (request.RequestUri?.PathAndQuery.Contains("/deduct") == true)
            {
                deductCalled = true;
                var dto = new InventoryItemDto
                {
                    Id = 1, ProductId = 1, ProductName = "Test",
                    QuantityOnHand = 95, ReorderLevel = 10
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(dto)
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.True(deductCalled, "Expected inventory-service deduct endpoint to be called");
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenInventoryServiceReturnsConflict()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient(request =>
        {
            if (request.RequestUri?.PathAndQuery.Contains("/deduct") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new { error = "Insufficient stock" })
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

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
