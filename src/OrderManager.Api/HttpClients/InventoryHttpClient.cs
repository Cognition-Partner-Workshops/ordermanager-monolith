using System.Net.Http.Json;

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

public class InventoryServiceException : Exception
{
    public int StatusCode { get; }
    public InventoryServiceException(string message, int statusCode = 503) : base(message)
    {
        StatusCode = statusCode;
    }
    public InventoryServiceException(string message, Exception inner, int statusCode = 503) : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

public class InventoryHttpClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<InventoryItemDto>> GetAllInventoryAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory");
            return result ?? new List<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryServiceException("Inventory service is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InventoryServiceException("Inventory service request timed out", ex);
        }
    }

    public async Task<InventoryItemDto?> GetInventoryByProductIdAsync(int productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/inventory/product/{productId}");
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryServiceException("Inventory service is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InventoryServiceException("Inventory service request timed out", ex);
        }
    }

    public async Task<InventoryItemDto?> RestockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/restock", new { Quantity = quantity });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new InventoryServiceException("Inventory service is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InventoryServiceException("Inventory service request timed out", ex);
        }
    }

    public async Task<InventoryItemDto?> DeductStockAsync(int productId, int quantity)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/inventory/product/{productId}/deduct", new { Quantity = quantity });
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                throw new InvalidOperationException(error?.Error ?? "Insufficient stock");
            }
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                throw new ArgumentException(error?.Error ?? $"No inventory record for product {productId}");
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InventoryItemDto>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new InventoryServiceException("Inventory service is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InventoryServiceException("Inventory service request timed out", ex);
        }
    }

    public async Task<List<InventoryItemDto>> GetLowStockItemsAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<InventoryItemDto>>("api/inventory/low-stock");
            return result ?? new List<InventoryItemDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryServiceException("Inventory service is unavailable", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InventoryServiceException("Inventory service request timed out", ex);
        }
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
