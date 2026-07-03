using System.Net;
using System.Net.Http.Json;

namespace OrderManager.Api.Clients;

public class ProductHttpClient : IProductClient
{
    private readonly HttpClient _httpClient;

    public ProductHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProductDto>> GetAllProductsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ProductDto>>("api/products")
            ?? new List<ProductDto>();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/products/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

    public async Task<List<ProductDto>> GetProductsByIdsAsync(IEnumerable<int> ids)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products/batch", new { Ids = ids.ToList() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ProductDto>>()) ?? new List<ProductDto>();
    }

    public async Task<List<ProductDto>> GetProductsByCategoryAsync(string category)
    {
        return await _httpClient.GetFromJsonAsync<List<ProductDto>>($"api/products/category/{Uri.EscapeDataString(category)}")
            ?? new List<ProductDto>();
    }

    public async Task<ProductDto> CreateProductAsync(ProductDto product)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products", product);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var badRequest = await ReadErrorAsync(response);
            throw new ArgumentException(badRequest ?? "Invalid product.");
        }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var conflict = await ReadErrorAsync(response);
            throw new InvalidOperationException(conflict ?? $"A product with SKU {product.Sku} already exists.");
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return body?.Error;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ErrorResponse
    {
        public string? Error { get; set; }
    }
}
