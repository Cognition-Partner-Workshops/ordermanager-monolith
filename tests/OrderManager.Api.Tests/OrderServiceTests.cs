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

    private static InventoryHttpClient CreateMockInventoryClient(bool hasStock = true, bool deductSucceeds = true)
    {
        var handler = new MockHttpMessageHandler(hasStock, deductSucceeds);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8081") };
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
        var inventoryClient = CreateMockInventoryClient(hasStock: true, deductSucceeds: true);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(5, order.Items.First().Quantity);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient(hasStock: false);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly bool _hasStock;
    private readonly bool _deductSucceeds;

    public MockHttpMessageHandler(bool hasStock = true, bool deductSucceeds = true)
    {
        _hasStock = hasStock;
        _deductSucceeds = deductSucceeds;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";

        if (path.Contains("/check") || path.Contains("/api/inventory/product/") && request.Method == HttpMethod.Get)
        {
            var dto = new InventoryItemDto
            {
                Id = 1, ProductId = 1, ProductName = "Test", Sku = "TST-001",
                QuantityOnHand = _hasStock ? 100 : 0, ReorderLevel = 10,
                WarehouseLocation = "A-01", LastRestocked = DateTime.UtcNow
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto)
            });
        }

        if (path.Contains("/deduct"))
        {
            if (!_deductSucceeds)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"Insufficient stock\"}")
                });
            }
            var dto = new InventoryItemDto
            {
                Id = 1, ProductId = 1, ProductName = "Test", Sku = "TST-001",
                QuantityOnHand = 95, ReorderLevel = 10,
                WarehouseLocation = "A-01", LastRestocked = DateTime.UtcNow
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto)
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
