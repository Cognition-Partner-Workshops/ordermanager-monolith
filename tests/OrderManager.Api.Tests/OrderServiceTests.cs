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

    private static InventoryServiceClient CreateMockInventoryClient(bool stockAvailable = true)
    {
        var handler = new MockHttpMessageHandler(stockAvailable);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5100")
        };
        return new InventoryServiceClient(httpClient);
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
        var inventoryClient = CreateMockInventoryClient(stockAvailable: true);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateMockInventoryClient(stockAvailable: false);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly bool _stockAvailable;

        public MockHttpMessageHandler(bool stockAvailable)
        {
            _stockAvailable = stockAvailable;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.PathAndQuery ?? "";

            if (uri.Contains("/check"))
            {
                var json = JsonSerializer.Serialize(new { productId = 1, quantity = 5, available = _stockAvailable });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (uri.Contains("/deduct"))
            {
                if (!_stockAvailable)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("{\"error\":\"Insufficient stock\"}", System.Text.Encoding.UTF8, "application/json")
                    });
                }

                var item = new { id = 1, productId = 1, productName = "Test Product", quantityOnHand = 95, reorderLevel = 10, warehouseLocation = "A1", lastRestocked = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(item);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
