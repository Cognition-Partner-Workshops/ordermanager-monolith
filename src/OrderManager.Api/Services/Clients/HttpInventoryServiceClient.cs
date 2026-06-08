using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services.Clients;

public class HttpInventoryServiceClient : IInventoryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpInventoryServiceClient> _logger;

    public HttpInventoryServiceClient(HttpClient httpClient, ILogger<HttpInventoryServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<InventoryItem?> GetInventoryByProductIdAsync(int productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Inventory service returned {StatusCode} for product {ProductId}",
                    response.StatusCode, productId);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<InventoryItem>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach inventory service for product {ProductId}", productId);
            throw new InvalidOperationException($"Inventory service unavailable for product {productId}", ex);
        }
    }

    public async Task<InventoryItem> DeductStockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/inventory/product/{productId}/deduct",
                new { Quantity = quantity });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Inventory deduction failed for product {ProductId}: {Error}",
                    productId, error);
                throw new InvalidOperationException(
                    $"Failed to deduct stock for product {productId}: {error}");
            }

            return await response.Content.ReadFromJsonAsync<InventoryItem>()
                ?? throw new InvalidOperationException($"Invalid response from inventory service for product {productId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach inventory service for deduction on product {ProductId}", productId);
            throw new InvalidOperationException($"Inventory service unavailable for product {productId}", ex);
        }
    }
}
