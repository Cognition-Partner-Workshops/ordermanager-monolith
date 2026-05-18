using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using OrderService.Api.Data;
using OrderService.Api.Models;
using OrderService.Api.Services;

namespace OrderService.Api.Tests;

public class OrderManagementServiceTests : IDisposable
{
    private readonly OrderDbContext _context;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly OrderManagementService _service;

    public OrderManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new OrderDbContext(options);
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _service = new OrderManagementService(_context, _httpClientFactoryMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsEmptyList_WhenNoOrders()
    {
        var result = await _service.GetAllOrdersAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsOrdersWithItems()
    {
        var order = new Order
        {
            CustomerId = 1,
            CustomerName = "Test Customer",
            Status = "Pending",
            TotalAmount = 50.00m,
            ShippingAddress = "123 Main St",
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, ProductName = "Widget", Quantity = 2, UnitPrice = 25.00m }
            }
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _service.GetAllOrdersAsync();

        Assert.Single(result);
        Assert.Single(result[0].Items);
        Assert.Equal("Test Customer", result[0].CustomerName);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ReturnsOrdersOrderedByDateDescending()
    {
        var older = new Order
        {
            CustomerId = 1,
            CustomerName = "Customer A",
            OrderDate = DateTime.UtcNow.AddDays(-2),
            TotalAmount = 10m,
            ShippingAddress = "Address A"
        };
        var newer = new Order
        {
            CustomerId = 2,
            CustomerName = "Customer B",
            OrderDate = DateTime.UtcNow,
            TotalAmount = 20m,
            ShippingAddress = "Address B"
        };
        _context.Orders.AddRange(older, newer);
        await _context.SaveChangesAsync();

        var result = await _service.GetAllOrdersAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result[0].OrderDate >= result[1].OrderDate);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetOrderByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsOrderWithItems()
    {
        var order = new Order
        {
            CustomerId = 1,
            CustomerName = "Test Customer",
            TotalAmount = 75.00m,
            ShippingAddress = "456 Oak Ave",
            Items = new List<OrderItem>
            {
                new() { ProductId = 1, ProductName = "Widget", Quantity = 3, UnitPrice = 25.00m }
            }
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _service.GetOrderByIdAsync(order.Id);

        Assert.NotNull(result);
        Assert.Equal(order.Id, result.Id);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task CreateOrderAsync_CreatesOrderSuccessfully()
    {
        var customer = new CustomerDto(1, "John Doe", "john@test.com", "555-0100", "123 Main St", "Springfield", "IL", "62701");
        var product = new ProductDto(1, "Widget", "A test widget", "Widgets", 25.00m, "WDG-001");
        var inventory = new InventoryDto(1, 1, 100, 10, "Warehouse A");

        SetupMockHttpClient("CustomerServiceClient", "api/customers/1", customer);
        SetupMockHttpClient("ProductServiceClient", "api/products/1", product);
        SetupMockHttpClient("InventoryServiceClient", "api/inventory/product/1", inventory);

        var items = new List<(int ProductId, int Quantity)> { (1, 2) };
        var result = await _service.CreateOrderAsync(1, items);

        Assert.Equal(1, result.CustomerId);
        Assert.Equal("John Doe", result.CustomerName);
        Assert.Equal("123 Main St, Springfield, IL 62701", result.ShippingAddress);
        Assert.Equal(50.00m, result.TotalAmount);
        Assert.Single(result.Items);
        Assert.Equal("Widget", result.Items.First().ProductName);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenCustomerNotFound()
    {
        SetupMockHttpClientWithStatus("CustomerServiceClient", "api/customers/999", HttpStatusCode.NotFound);

        var items = new List<(int ProductId, int Quantity)> { (1, 2) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateOrderAsync(999, items));
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenProductNotFound()
    {
        var customer = new CustomerDto(1, "John Doe", "john@test.com", "555-0100", "123 Main St", "Springfield", "IL", "62701");
        SetupMockHttpClient("CustomerServiceClient", "api/customers/1", customer);
        SetupMockHttpClientWithStatus("ProductServiceClient", "api/products/1", HttpStatusCode.NotFound);

        var items = new List<(int ProductId, int Quantity)> { (1, 2) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateOrderAsync(1, items));
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenInsufficientStock()
    {
        var customer = new CustomerDto(1, "John Doe", "john@test.com", "555-0100", "123 Main St", "Springfield", "IL", "62701");
        var product = new ProductDto(1, "Widget", "A test widget", "Widgets", 25.00m, "WDG-001");
        var inventory = new InventoryDto(1, 1, 1, 10, "Warehouse A");

        SetupMockHttpClient("CustomerServiceClient", "api/customers/1", customer);
        SetupMockHttpClient("ProductServiceClient", "api/products/1", product);
        SetupMockHttpClient("InventoryServiceClient", "api/inventory/product/1", inventory);

        var items = new List<(int ProductId, int Quantity)> { (1, 5) };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateOrderAsync(1, items));
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_UpdatesStatusSuccessfully()
    {
        var order = new Order
        {
            CustomerId = 1,
            CustomerName = "Test Customer",
            Status = "Pending",
            TotalAmount = 50.00m,
            ShippingAddress = "123 Main St"
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var result = await _service.UpdateOrderStatusAsync(order.Id, "Shipped");

        Assert.Equal("Shipped", result.Status);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ThrowsWhenOrderNotFound()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateOrderStatusAsync(999, "Shipped"));
    }

    private void SetupMockHttpClient<T>(string clientName, string requestPath, T responseObject)
    {
        var handler = new MockHttpMessageHandler(requestPath, responseObject!);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        _httpClientFactoryMock.Setup(f => f.CreateClient(clientName)).Returns(client);
    }

    private void SetupMockHttpClientWithStatus(string clientName, string requestPath, HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler(requestPath, statusCode);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        _httpClientFactoryMock.Setup(f => f.CreateClient(clientName)).Returns(client);
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _expectedPath;
    private readonly object? _responseObject;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string expectedPath, object responseObject)
    {
        _expectedPath = expectedPath;
        _responseObject = responseObject;
        _statusCode = HttpStatusCode.OK;
    }

    public MockHttpMessageHandler(string expectedPath, HttpStatusCode statusCode)
    {
        _expectedPath = expectedPath;
        _responseObject = null;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_responseObject == null || _statusCode != HttpStatusCode.OK)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }

        var json = JsonSerializer.Serialize(_responseObject, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
