using System.Net.Http.Json;
using OrderManager.Api.Interfaces;
using OrderManager.Api.Models;

namespace OrderManager.Api.Proxies;

public class OrderServiceProxy : IOrderService
{
    private readonly HttpClient _httpClient;

    public OrderServiceProxy(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Order>>("api/orders") ?? new();
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/orders/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Order>();
    }

    public async Task<Order> CreateOrderAsync(int customerId, List<(int ProductId, int Quantity)> items)
    {
        var request = new
        {
            CustomerId = customerId,
            Items = items.Select(i => new { i.ProductId, i.Quantity }).ToList()
        };
        var response = await _httpClient.PostAsJsonAsync("api/orders", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>()
            ?? throw new InvalidOperationException("Failed to deserialize order response");
    }

    public async Task<Order> UpdateOrderStatusAsync(int orderId, string status)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/orders/{orderId}/status", new { Status = status });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>()
            ?? throw new InvalidOperationException("Failed to deserialize order response");
    }
}
