using System.Net;
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

    private static InventoryServiceClient CreateInventoryClient(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        InventoryItem? responseItem = null)
    {
        var handler = new MockHttpHandler(statusCode, responseItem);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5100") };
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
    public async Task CreateOrder_CallsInventoryServiceToDeductStock()
    {
        using var context = CreateContext();
        var deductedItem = new InventoryItem { Id = 1, ProductId = 1, QuantityOnHand = 45 };
        var inventoryClient = CreateInventoryClient(HttpStatusCode.OK, deductedItem);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        Assert.Equal(product.Id, order.Items.First().ProductId);
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenInventoryServiceReturnsConflict()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(HttpStatusCode.Conflict);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly InventoryItem? _responseItem;

        public MockHttpHandler(HttpStatusCode statusCode, InventoryItem? responseItem)
        {
            _statusCode = statusCode;
            _responseItem = responseItem;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_statusCode == HttpStatusCode.Conflict)
            {
                response.Content = new StringContent(
                    JsonSerializer.Serialize(new { error = "Insufficient stock" }),
                    System.Text.Encoding.UTF8, "application/json");
            }
            else if (_responseItem is not null)
            {
                response.Content = new StringContent(
                    JsonSerializer.Serialize(_responseItem),
                    System.Text.Encoding.UTF8, "application/json");
            }
            return Task.FromResult(response);
        }
    }
}
