using System.Net.Http.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryServiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryServiceHttpClient> _logger;

    public InventoryServiceHttpClient(HttpClient httpClient, ILogger<InventoryServiceHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory");
            return items ?? new List<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch inventory from inventory-service");
            throw new InvalidOperationException("Inventory service is unavailable", ex);
        }
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch inventory for product {ProductId} from inventory-service", productId);
            throw new InvalidOperationException("Inventory service is unavailable", ex);
        }
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/inventory/product/{productId}/restock",
                new { Quantity = quantity });

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>()
                ?? throw new InvalidOperationException("Empty response from inventory-service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to restock product {ProductId} via inventory-service", productId);
            throw new InvalidOperationException("Inventory service is unavailable", ex);
        }
    }

    public async Task<InventoryItemDto?> DeductStockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/inventory/product/{productId}/deduct",
                new { Quantity = quantity });

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                throw new InvalidOperationException(error?.Error ?? "Insufficient stock");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new ArgumentException($"No inventory record for product {productId}");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to deduct stock for product {ProductId} via inventory-service", productId);
            throw new InvalidOperationException("Inventory service is unavailable", ex);
        }
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory/low-stock");
            return items ?? new List<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch low-stock items from inventory-service");
            throw new InvalidOperationException("Inventory service is unavailable", ex);
        }
    }
}

public class InventoryItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
