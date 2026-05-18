using System.Net.Http.Json;
using OrderManager.Api.Interfaces;
using OrderManager.Api.Models;

namespace OrderManager.Api.Proxies;

public class CustomerServiceProxy : ICustomerService
{
    private readonly HttpClient _httpClient;

    public CustomerServiceProxy(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Customer>>("api/customers") ?? new();
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/customers/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Customer>();
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        var response = await _httpClient.PostAsJsonAsync("api/customers", customer);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Customer>()
            ?? throw new InvalidOperationException("Failed to deserialize customer response");
    }
}
