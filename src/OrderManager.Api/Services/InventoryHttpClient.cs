using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class InventoryHttpClient : IInventoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<InventoryHttpClient> _logger;

    public InventoryHttpClient(HttpClient http, ILogger<InventoryHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<List<InventoryItemDto>> GetAllAsync(CancellationToken ct = default) =>
        GetListAsync("api/inventory", ct);

    public Task<List<InventoryItemDto>> GetLowStockAsync(CancellationToken ct = default) =>
        GetListAsync("api/inventory/low-stock", ct);

    public async Task<InventoryItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => _http.GetAsync($"api/inventory/product/{productId}", ct));
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions, ct);
    }

    public Task<InventoryItemDto> RestockAsync(int productId, int quantity, CancellationToken ct = default) =>
        PostQuantityAsync($"api/inventory/product/{productId}/restock", productId, quantity, ct);

    public Task<InventoryItemDto> DeductAsync(int productId, int quantity, CancellationToken ct = default) =>
        PostQuantityAsync($"api/inventory/product/{productId}/deduct", productId, quantity, ct);

    public async Task<StockCheckResponse> CheckAvailabilityAsync(IEnumerable<StockCheckLine> items, CancellationToken ct = default)
    {
        using var response = await SendAsync(() =>
            _http.PostAsJsonAsync("api/inventory/check", new StockCheckRequest(items.ToList()), JsonOptions, ct));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<StockCheckResponse>(JsonOptions, ct)
            ?? throw new InventoryServiceUnavailableException("Inventory service returned an empty availability response");
    }

    private async Task<List<InventoryItemDto>> GetListAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.GetAsync(path, ct));
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<List<InventoryItemDto>>(JsonOptions, ct) ?? new();
    }

    private async Task<InventoryItemDto> PostQuantityAsync(string path, int productId, int quantity, CancellationToken ct)
    {
        using var response = await SendAsync(() => _http.PostAsJsonAsync(path, new { quantity }, JsonOptions, ct));

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ArgumentException($"No inventory record for product {productId}");

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ReadErrorAsync(response, ct) ?? $"Inventory operation rejected for product {productId}");

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<InventoryItemDto>(JsonOptions, ct)
            ?? throw new InventoryServiceUnavailableException("Inventory service returned an empty response");
    }

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Inventory service call to {BaseAddress} failed", _http.BaseAddress);
            throw new InventoryServiceUnavailableException("Inventory service is unavailable", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await ReadErrorAsync(response, ct);
        throw new InventoryServiceUnavailableException(
            $"Inventory service returned {(int)response.StatusCode}{(detail is null ? string.Empty : $": {detail}")}");
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            if (body.ValueKind == JsonValueKind.Object)
            {
                if (body.TryGetProperty("error", out var error)) return error.GetString();
                if (body.TryGetProperty("title", out var title)) return title.GetString();
            }
        }
        catch (JsonException) { }
        return null;
    }
}
