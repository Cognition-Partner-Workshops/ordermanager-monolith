using Microsoft.EntityFrameworkCore;
using ProductService.Api.Data;
using ProductService.Api.Interfaces;
using ProductService.Api.Models;

namespace ProductService.Api.Services;

public class ProductServiceImpl : IProductService
{
    private readonly ProductDbContext _context;

    public ProductServiceImpl(ProductDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _context.Products.ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(string category)
    {
        return await _context.Products.Where(p => p.Category == category).ToListAsync();
    }
}
