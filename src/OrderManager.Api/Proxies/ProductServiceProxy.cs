using System.Net.Http.Json;
using OrderManager.Api.Interfaces;
using OrderManager.Api.Models;

namespace OrderManager.Api.Proxies;

public class ProductServiceProxy : IProductService
{
    private readonly HttpClient _httpClient;

    public ProductServiceProxy(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Product>>("api/products") ?? new();
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/products/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products", product);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>()
            ?? throw new InvalidOperationException("Failed to deserialize product response");
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(string category)
    {
        return await _httpClient.GetFromJsonAsync<List<Product>>($"api/products/category/{category}") ?? new();
    }
}
