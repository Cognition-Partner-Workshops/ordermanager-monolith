using System.Net.Http.Json;

namespace OrderManager.Api.Services;

public class InventoryHttpClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> CheckStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}/check?quantity={quantity}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CheckStockResponse>();
        return result?.Available ?? false;
    }

    public async Task DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
    }
}

public record CheckStockResponse(int ProductId, int Quantity, bool Available);
