using Microsoft.EntityFrameworkCore;
using Xunit;
using InventoryService.Api.Data;
using InventoryService.Api.Models;
using InventoryService.Api.Services;

namespace InventoryService.Api.Tests;

public class InventoryServiceTests : IDisposable
{
    private readonly InventoryDbContext _context;
    private readonly InventoryServiceImpl _service;

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new InventoryDbContext(options);
        SeedTestData();
        _service = new InventoryServiceImpl(_context);
    }

    private void SeedTestData()
    {
        _context.InventoryItems.AddRange(
            new InventoryItem { Id = 1, ProductId = 1, ProductName = "Widget A", QuantityOnHand = 50, ReorderLevel = 10, WarehouseLocation = "A-01" },
            new InventoryItem { Id = 2, ProductId = 2, ProductName = "Widget B", QuantityOnHand = 100, ReorderLevel = 10, WarehouseLocation = "A-02" },
            new InventoryItem { Id = 3, ProductId = 3, ProductName = "Gadget X", QuantityOnHand = 5, ReorderLevel = 10, WarehouseLocation = "A-03" }
        );
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAllInventoryAsync_ReturnsAllItems()
    {
        var result = await _service.GetAllInventoryAsync();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_ExistingProduct_ReturnsItem()
    {
        var result = await _service.GetInventoryByProductIdAsync(1);
        Assert.NotNull(result);
        Assert.Equal(1, result.ProductId);
        Assert.Equal("Widget A", result.ProductName);
        Assert.Equal(50, result.QuantityOnHand);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_NonExistingProduct_ReturnsNull()
    {
        var result = await _service.GetInventoryByProductIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task RestockAsync_IncreasesQuantityAndUpdatesTimestamp()
    {
        var before = DateTime.UtcNow;
        var result = await _service.RestockAsync(1, 25);
        Assert.Equal(75, result.QuantityOnHand);
        Assert.True(result.LastRestocked >= before);
    }

    [Fact]
    public async Task RestockAsync_NonExistingProduct_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RestockAsync(999, 10));
    }

    [Fact]
    public async Task GetLowStockItemsAsync_ReturnsOnlyLowStockItems()
    {
        var result = await _service.GetLowStockItemsAsync();
        Assert.Single(result);
        Assert.Equal(3, result[0].ProductId);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
