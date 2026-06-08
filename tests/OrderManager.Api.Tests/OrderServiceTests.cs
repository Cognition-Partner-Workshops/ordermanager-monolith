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

    private static InventoryServiceHttpClient CreateMockInventoryClient(
        bool stockAvailable = true, int quantityOnHand = 100)
    {
        var handler = new MockHttpMessageHandler(stockAvailable, quantityOnHand);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        return new InventoryServiceHttpClient(httpClient);
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
    public async Task CreateOrder_SucceedsWithSufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient(stockAvailable: true, quantityOnHand: 100);
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
        var inventoryClient = CreateMockInventoryClient(stockAvailable: false, quantityOnHand: 0);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

/// <summary>
/// Mock HTTP handler that simulates inventory-service responses for unit testing.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly bool _stockAvailable;
    private readonly int _quantityOnHand;

    public MockHttpMessageHandler(bool stockAvailable, int quantityOnHand)
    {
        _stockAvailable = stockAvailable;
        _quantityOnHand = quantityOnHand;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";

        // GET api/inventory/product/{id} — used by CheckStockAsync
        if (request.Method == HttpMethod.Get && path.Contains("api/inventory/product/"))
        {
            if (!_stockAvailable && _quantityOnHand <= 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var dto = new InventoryItemDto
            {
                Id = 1,
                ProductId = 1,
                ProductName = "Test Product",
                QuantityOnHand = _quantityOnHand,
                ReorderLevel = 10,
                WarehouseLocation = "A-01",
                LastRestocked = DateTime.UtcNow
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto)
            };
            return Task.FromResult(response);
        }

        // POST api/inventory/product/{id}/deduct
        if (request.Method == HttpMethod.Post && path.Contains("/deduct"))
        {
            if (!_stockAvailable)
            {
                var errorResponse = new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("{\"error\":\"Insufficient stock\"}")
                };
                return Task.FromResult(errorResponse);
            }

            var dto = new InventoryItemDto
            {
                Id = 1,
                ProductId = 1,
                ProductName = "Test Product",
                QuantityOnHand = _quantityOnHand - 5,
                ReorderLevel = 10,
                WarehouseLocation = "A-01",
                LastRestocked = DateTime.UtcNow
            };
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(dto)
            };
            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
