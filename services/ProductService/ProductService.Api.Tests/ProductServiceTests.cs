using Microsoft.EntityFrameworkCore;
using ProductService.Api.Data;
using ProductService.Api.Models;
using ProductService.Api.Services;
using Xunit;

namespace ProductService.Api.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly ProductDbContext _context;
    private readonly ProductServiceImpl _service;

    public ProductServiceTests()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ProductDbContext(options);
        _service = new ProductServiceImpl(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsEmpty_WhenNoProducts()
    {
        var result = await _service.GetAllProductsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllProductsAsync_ReturnsAllProducts()
    {
        _context.Products.AddRange(
            new Product { Name = "Widget A", Sku = "WGT-001", Category = "Widgets", Price = 9.99m, Description = "Standard widget" },
            new Product { Name = "Widget B", Sku = "WGT-002", Category = "Widgets", Price = 19.99m, Description = "Premium widget" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAllProductsAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsProduct_WhenExists()
    {
        var product = new Product { Name = "Widget A", Sku = "WGT-001", Category = "Widgets", Price = 9.99m, Description = "Standard widget" };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var result = await _service.GetProductByIdAsync(product.Id);
        Assert.NotNull(result);
        Assert.Equal("Widget A", result.Name);
        Assert.Equal("WGT-001", result.Sku);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsNull_WhenNotExists()
    {
        var result = await _service.GetProductByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateProductAsync_AddsProductToDatabase()
    {
        var product = new Product { Name = "Gadget X", Sku = "GDG-001", Category = "Gadgets", Price = 29.99m, Description = "Basic gadget" };

        var result = await _service.CreateProductAsync(product);

        Assert.True(result.Id > 0);
        Assert.Equal("Gadget X", result.Name);
        Assert.Single(_context.Products);
    }

    [Fact]
    public async Task GetProductsByCategoryAsync_ReturnsMatchingProducts()
    {
        _context.Products.AddRange(
            new Product { Name = "Widget A", Sku = "WGT-001", Category = "Widgets", Price = 9.99m, Description = "Standard widget" },
            new Product { Name = "Widget B", Sku = "WGT-002", Category = "Widgets", Price = 19.99m, Description = "Premium widget" },
            new Product { Name = "Gadget X", Sku = "GDG-001", Category = "Gadgets", Price = 29.99m, Description = "Basic gadget" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetProductsByCategoryAsync("Widgets");
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal("Widgets", p.Category));
    }

    [Fact]
    public async Task GetProductsByCategoryAsync_ReturnsEmpty_WhenNoneMatch()
    {
        _context.Products.Add(
            new Product { Name = "Widget A", Sku = "WGT-001", Category = "Widgets", Price = 9.99m, Description = "Standard widget" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetProductsByCategoryAsync("NonExistent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task SeedData_Initialize_SeedsFiveProducts()
    {
        SeedData.Initialize(_context);

        var products = await _context.Products.ToListAsync();
        Assert.Equal(5, products.Count);
        Assert.Contains(products, p => p.Name == "Widget A" && p.Sku == "WGT-001");
        Assert.Contains(products, p => p.Name == "Widget B" && p.Sku == "WGT-002");
        Assert.Contains(products, p => p.Name == "Gadget X" && p.Sku == "GDG-001");
        Assert.Contains(products, p => p.Name == "Gadget Y" && p.Sku == "GDG-002");
        Assert.Contains(products, p => p.Name == "Thingamajig" && p.Sku == "THG-001");
    }

    [Fact]
    public async Task SeedData_Initialize_DoesNotDuplicateOnSecondCall()
    {
        SeedData.Initialize(_context);
        SeedData.Initialize(_context);

        var products = await _context.Products.ToListAsync();
        Assert.Equal(5, products.Count);
    }
}
