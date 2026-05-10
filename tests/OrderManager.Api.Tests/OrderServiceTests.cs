using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    private static InventoryApiClient CreateMockInventoryClient(
        int stockQuantity = 100,
        bool deductSucceeds = true)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        // Mock GET api/inventory/product/{id} - return stock quantity
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Get &&
                    r.RequestUri!.PathAndQuery.Contains("/api/inventory/product/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(new InventoryItemDto(
                    1, 1, "Test Product", stockQuantity, 10, "A-01", DateTime.UtcNow))
            });

        // Mock POST api/inventory/product/{id}/deduct
        if (deductSucceeds)
        {
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.Method == HttpMethod.Post &&
                        r.RequestUri!.PathAndQuery.Contains("/deduct")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(new InventoryItemDto(
                        1, 1, "Test Product", stockQuantity - 5, 10, "A-01", DateTime.UtcNow))
                });
        }
        else
        {
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r =>
                        r.Method == HttpMethod.Post &&
                        r.RequestUri!.PathAndQuery.Contains("/deduct")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Conflict,
                    Content = JsonContent.Create(new { error = "Insufficient stock" })
                });
        }

        var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:5001")
        };
        return new InventoryApiClient(httpClient);
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
        var inventoryClient = CreateMockInventoryClient(stockQuantity: 100);
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
        var inventoryClient = CreateMockInventoryClient(stockQuantity: 2, deductSucceeds: false);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
