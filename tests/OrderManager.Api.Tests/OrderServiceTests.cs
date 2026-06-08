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

    private static Mock<IInventoryServiceClient> CreateMockInventoryClient(int quantityOnHand = 100, bool deductSuccess = true)
    {
        var mock = new Mock<IInventoryServiceClient>();
        mock.Setup(x => x.GetStockAsync(It.IsAny<int>()))
            .ReturnsAsync((int productId) => new InventoryStockResponse(productId, quantityOnHand));
        mock.Setup(x => x.DeductStockAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(deductSuccess);
        return mock;
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var mockClient = CreateMockInventoryClient();
        var service = new OrderService(context, mockClient.Object);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_CallsInventoryService()
    {
        using var context = CreateContext();
        var mockClient = CreateMockInventoryClient(quantityOnHand: 100);
        var service = new OrderService(context, mockClient.Object);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        mockClient.Verify(x => x.GetStockAsync(product.Id), Times.Once);
        mockClient.Verify(x => x.DeductStockAsync(product.Id, 5), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var mockClient = CreateMockInventoryClient(quantityOnHand: 2);
        var service = new OrderService(context, mockClient.Object);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenDeductFails()
    {
        using var context = CreateContext();
        var mockClient = CreateMockInventoryClient(quantityOnHand: 100, deductSuccess: false);
        var service = new OrderService(context, mockClient.Object);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) }));
    }

    [Fact]
    public async Task CreateOrder_ThrowsWhenNoInventoryRecord()
    {
        using var context = CreateContext();
        var mockClient = new Mock<IInventoryServiceClient>();
        mockClient.Setup(x => x.GetStockAsync(It.IsAny<int>()))
            .ReturnsAsync((InventoryStockResponse?)null);
        var service = new OrderService(context, mockClient.Object);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) }));
    }
}
