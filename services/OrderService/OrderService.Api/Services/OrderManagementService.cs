using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using OrderService.Api.Models;

namespace OrderService.Api.Services;

public interface IOrderManagementService
{
    Task<List<Order>> GetAllOrdersAsync();
    Task<Order?> GetOrderByIdAsync(int id);
    Task<Order> CreateOrderAsync(int customerId, List<(int ProductId, int Quantity)> items);
    Task<Order> UpdateOrderStatusAsync(int orderId, string status);
}

public class OrderManagementService : IOrderManagementService
{
    private readonly OrderDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderManagementService(OrderDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order> CreateOrderAsync(int customerId, List<(int ProductId, int Quantity)> items)
    {
        var customerClient = _httpClientFactory.CreateClient("CustomerServiceClient");
        var customerResponse = await customerClient.GetAsync($"api/customers/{customerId}");
        if (!customerResponse.IsSuccessStatusCode)
            throw new ArgumentException($"Customer {customerId} not found");
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>()
            ?? throw new ArgumentException($"Customer {customerId} not found");

        var order = new Order
        {
            CustomerId = customerId,
            CustomerName = customer.Name,
            ShippingAddress = $"{customer.Address}, {customer.City}, {customer.State} {customer.ZipCode}"
        };

        var productClient = _httpClientFactory.CreateClient("ProductServiceClient");
        var inventoryClient = _httpClientFactory.CreateClient("InventoryServiceClient");

        foreach (var (productId, quantity) in items)
        {
            var productResponse = await productClient.GetAsync($"api/products/{productId}");
            if (!productResponse.IsSuccessStatusCode)
                throw new ArgumentException($"Product {productId} not found");
            var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>()
                ?? throw new ArgumentException($"Product {productId} not found");

            var inventoryResponse = await inventoryClient.GetAsync($"api/inventory/product/{productId}");
            if (!inventoryResponse.IsSuccessStatusCode)
                throw new InvalidOperationException($"No inventory record for product {productId}");
            var inventory = await inventoryResponse.Content.ReadFromJsonAsync<InventoryDto>();
            if (inventory != null && inventory.QuantityOnHand < quantity)
                throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {inventory.QuantityOnHand}");

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Order> UpdateOrderStatusAsync(int orderId, string status)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new ArgumentException($"Order {orderId} not found");
        order.Status = status;
        await _context.SaveChangesAsync();
        return order;
    }
}

public record CustomerDto(int Id, string Name, string Email, string Phone, string Address, string City, string State, string ZipCode);
public record ProductDto(int Id, string Name, string Description, string Category, decimal Price, string Sku);
public record InventoryDto(int Id, int ProductId, int QuantityOnHand, int ReorderLevel, string WarehouseLocation);
