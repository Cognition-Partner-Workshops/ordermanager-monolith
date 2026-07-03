using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Clients;
using OrderManager.Api.Data;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class OrderService
{
    private readonly AppDbContext _context;
    private readonly IInventoryClient _inventoryClient;
    private readonly IProductClient _productClient;

    public OrderService(AppDbContext context, IInventoryClient inventoryClient, IProductClient productClient)
    {
        _context = context;
        _inventoryClient = inventoryClient;
        _productClient = productClient;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order> CreateOrderAsync(int customerId, List<(int ProductId, int Quantity)> items)
    {
        var customer = await _context.Customers.FindAsync(customerId)
            ?? throw new ArgumentException($"Customer {customerId} not found");

        var order = new Order
        {
            CustomerId = customerId,
            ShippingAddress = $"{customer.Address}, {customer.City}, {customer.State} {customer.ZipCode}"
        };

        var products = (await _productClient.GetProductsByIdsAsync(items.Select(i => i.ProductId)))
            .ToDictionary(p => p.Id);

        foreach (var (productId, quantity) in items)
        {
            if (!products.TryGetValue(productId, out var product))
                throw new ArgumentException($"Product {productId} not found");

            var inventory = await _inventoryClient.GetInventoryByProductIdAsync(productId)
                ?? throw new InvalidOperationException($"No inventory record for product {productId}");

            if (inventory.QuantityOnHand < quantity)
                throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {inventory.QuantityOnHand}");

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }

        var deducted = new List<(int ProductId, int Quantity)>();
        try
        {
            foreach (var item in order.Items)
            {
                await _inventoryClient.DeductAsync(item.ProductId, item.Quantity);
                deducted.Add((item.ProductId, item.Quantity));
            }

            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }
        catch
        {
            foreach (var (productId, quantity) in deducted)
            {
                try
                {
                    await _inventoryClient.RestockAsync(productId, quantity);
                }
                catch
                {
                    // Compensation is best-effort; surface the original failure.
                }
            }
            throw;
        }
    }

    public async Task<Order> UpdateOrderStatusAsync(int orderId, string status)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new ArgumentException($"Order {orderId} not found");
        order.Status = status;
        await _context.SaveChangesAsync();
        return order;
    }
}
