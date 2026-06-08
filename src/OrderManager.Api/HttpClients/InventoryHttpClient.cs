using System.Net.Http.Json;
using System.Text.Json;

namespace OrderManager.Api.HttpClients;

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

public class InventoryHttpClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        var response = await _httpClient.GetAsync("api/inventory");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(JsonOptions) ?? new();
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions);
    }

    public async Task<InventoryItemDto> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/restock",
            new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize restock response");
    }

    public async Task<InventoryItemDto> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/inventory/product/{productId}/deduct",
            new { Quantity = quantity });

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            var message = error.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString() ?? "Insufficient stock"
                : "Insufficient stock";
            throw new InvalidOperationException(message);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            var message = error.TryGetProperty("error", out var errorProp)
                ? errorProp.GetString() ?? $"No inventory record for product {productId}"
                : $"No inventory record for product {productId}";
            throw new ArgumentException(message);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize deduct response");
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        var response = await _httpClient.GetAsync("api/inventory/low-stock");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(JsonOptions) ?? new();
    }
}
