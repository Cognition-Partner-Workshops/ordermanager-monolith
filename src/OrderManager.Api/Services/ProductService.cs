using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class ProductService
{
    private readonly AppDbContext _context;
    private readonly IInventoryClient _inventory;
    private readonly ILogger<ProductService> _logger;

    public ProductService(AppDbContext context, IInventoryClient inventory, ILogger<ProductService> logger)
    {
        _context = context;
        _inventory = inventory;
        _logger = logger;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        var products = await _context.Products.ToListAsync();
        await AttachInventoryAsync(products);
        return products;
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is not null) await AttachInventoryAsync(new[] { product });
        return product;
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(string category)
    {
        var products = await _context.Products.Where(p => p.Category == category).ToListAsync();
        await AttachInventoryAsync(products);
        return products;
    }

    // Stock levels live in the inventory-service; product reads degrade gracefully if it is unreachable.
    private async Task AttachInventoryAsync(IReadOnlyCollection<Product> products)
    {
        if (products.Count == 0) return;
        try
        {
            var byProduct = (await _inventory.GetAllAsync()).ToDictionary(i => i.ProductId);
            foreach (var product in products)
                product.Inventory = byProduct.GetValueOrDefault(product.Id);
        }
        catch (InventoryServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Inventory service unavailable; returning products without stock levels");
        }
    }
}
