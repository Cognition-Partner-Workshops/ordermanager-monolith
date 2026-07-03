using System.Net;
using System.Net.Http.Json;

namespace OrderManager.Api.Clients;

public class CustomerHttpClient : ICustomerClient
{
    private readonly HttpClient _httpClient;

    public CustomerHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CustomerDto>> GetAllCustomersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CustomerDto>>("api/customers")
            ?? new List<CustomerDto>();
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id)
    {
        var response = await _httpClient.GetAsync($"api/customers/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerDto>();
    }

    public async Task<CustomerDto> CreateCustomerAsync(CustomerDto customer)
    {
        var response = await _httpClient.PostAsJsonAsync("api/customers", customer);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException("Invalid customer data.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException($"A customer with email {customer.Email} already exists.");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }
}
