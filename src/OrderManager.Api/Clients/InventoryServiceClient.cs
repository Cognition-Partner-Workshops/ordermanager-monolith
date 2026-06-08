using System.Net.Http.Json;

namespace OrderManager.Api.Clients;

public interface IInventoryServiceClient
{
    Task<InventoryStockResponse?> GetStockAsync(int productId);
    Task<bool> DeductStockAsync(int productId, int quantity);
}

public class InventoryServiceClient : IInventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<InventoryStockResponse?> GetStockAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"/api/inventory/product/{productId}/stock");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InventoryStockResponse>();
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/inventory/product/{productId}/deduct",
            new { Quantity = quantity });
        return response.IsSuccessStatusCode;
    }
}

public record InventoryStockResponse(int ProductId, int QuantityOnHand);
