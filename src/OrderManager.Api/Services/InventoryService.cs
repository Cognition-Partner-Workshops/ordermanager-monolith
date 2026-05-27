using System.Net.Http.Json;

namespace OrderManager.Api.Services;

public class InventoryServiceClient
{
    private readonly HttpClient _httpClient;

    public InventoryServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryDto>> GetAllInventoryAsync()
    {
        var response = await _httpClient.GetAsync("/api/inventory");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryDto>>() ?? new List<InventoryDto>();
    }

    public async Task<InventoryDto?> GetInventoryByProductIdAsync(int productId)
    {
        var response = await _httpClient.GetAsync($"/api/inventory/product/{productId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryDto>();
    }

    public async Task<InventoryDto> RestockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/inventory/product/{productId}/restock", new { Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryDto>()
            ?? throw new InvalidOperationException("Failed to deserialize restock response");
    }

    public async Task<bool> CheckStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.GetAsync($"/api/inventory/product/{productId}/check?quantity={quantity}");
        if (!response.IsSuccessStatusCode) return false;
        var result = await response.Content.ReadFromJsonAsync<CheckStockResponse>();
        return result?.Available ?? false;
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/inventory/product/{productId}/deduct", new { Quantity = quantity });
        return response.IsSuccessStatusCode;
    }

    public async Task<List<InventoryDto>> GetLowStockItemsAsync()
    {
        var response = await _httpClient.GetAsync("/api/inventory/low-stock");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InventoryDto>>() ?? new List<InventoryDto>();
    }
}

public class InventoryDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}

public class CheckStockResponse
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public bool Available { get; set; }
}
