using ProductService.Api.Models;

namespace ProductService.Api.Interfaces;

public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product> CreateProductAsync(Product product);
    Task<List<Product>> GetProductsByCategoryAsync(string category);
}
