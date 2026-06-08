using Microsoft.EntityFrameworkCore;
using Moq;
using OrderManager.Api.Clients;
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

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var mockClient = new Mock<IInventoryServiceClient>();
        var service = new OrderService(context, mockClient.Object);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_DeductsInventory()
    {
        using var context = CreateContext();
        var mockClient = new Mock<IInventoryServiceClient>();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        mockClient.Setup(c => c.CheckStockAsync(product.Id, 5)).ReturnsAsync(true);
        mockClient.Setup(c => c.DeductStockAsync(product.Id, 5)).Returns(Task.CompletedTask);

        var service = new OrderService(context, mockClient.Object);
        var order = await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        Assert.NotNull(order);
        Assert.Single(order.Items);
        mockClient.Verify(c => c.CheckStockAsync(product.Id, 5), Times.Once);
        mockClient.Verify(c => c.DeductStockAsync(product.Id, 5), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var mockClient = new Mock<IInventoryServiceClient>();
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        mockClient.Setup(c => c.CheckStockAsync(product.Id, 99999)).ReturnsAsync(false);

        var service = new OrderService(context, mockClient.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}

public class InventoryServiceClientTests
{
    [Fact]
    public async Task GetAllInventoryAsync_ReturnsItems()
    {
        var items = new List<InventoryItemDto>
        {
            new(1, 1, "Widget A", 100, 10, "A-01", DateTime.UtcNow),
            new(2, 2, "Widget B", 200, 10, "A-02", DateTime.UtcNow)
        };
        var handler = new MockHttpMessageHandler(System.Net.HttpStatusCode.OK,
            System.Text.Json.JsonSerializer.Serialize(items));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryServiceClient>.Instance;
        var client = new InventoryServiceClient(httpClient, logger);

        var result = await client.GetAllInventoryAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CheckStockAsync_ReturnsTrueWhenAvailable()
    {
        var response = new { available = true, quantityOnHand = 100 };
        var handler = new MockHttpMessageHandler(System.Net.HttpStatusCode.OK,
            System.Text.Json.JsonSerializer.Serialize(response));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryServiceClient>.Instance;
        var client = new InventoryServiceClient(httpClient, logger);

        var result = await client.CheckStockAsync(1, 10);
        Assert.True(result);
    }

    [Fact]
    public async Task DeductStockAsync_ThrowsOnFailure()
    {
        var handler = new MockHttpMessageHandler(System.Net.HttpStatusCode.BadRequest, "Insufficient stock");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryServiceClient>.Instance;
        var client = new InventoryServiceClient(httpClient, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeductStockAsync(1, 99999));
    }
}

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly System.Net.HttpStatusCode _statusCode;
    private readonly string _content;

    public MockHttpMessageHandler(System.Net.HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
