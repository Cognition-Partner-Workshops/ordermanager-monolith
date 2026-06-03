using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManager.Api.Data;
using OrderManager.Api.HttpClients;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class OrderService
{
    private readonly AppDbContext _context;
    private readonly InventoryHttpClient _inventoryClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext context, InventoryHttpClient inventoryClient, ILogger<OrderService> logger)
    {
        _context = context;
        _inventoryClient = inventoryClient;
        _logger = logger;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items).ThenInclude(i => i.Product)
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

        // Build order items first (validate products exist)
        foreach (var (productId, quantity) in items)
        {
            var product = await _context.Products.FindAsync(productId)
                ?? throw new ArgumentException($"Product {productId} not found");

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Deduct stock via inventory-service, then persist order.
        // Track successful deductions so we can compensate on failure.
        var deducted = new List<(int ProductId, int Quantity)>();
        try
        {
            foreach (var (productId, quantity) in items)
            {
                await _inventoryClient.DeductStockAsync(productId, quantity);
                deducted.Add((productId, quantity));
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex) when (deducted.Count > 0)
        {
            // Compensate: restock any items that were already deducted
            _logger.LogError(ex, "Order creation failed after deducting {Count} items, compensating", deducted.Count);
            foreach (var (productId, quantity) in deducted)
            {
                try
                {
                    await _inventoryClient.RestockAsync(productId, quantity);
                }
                catch (Exception restockEx)
                {
                    _logger.LogError(restockEx, "Compensation restock failed for product {ProductId}, qty {Qty}", productId, quantity);
                }
            }
            throw;
        }

        return order;
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
