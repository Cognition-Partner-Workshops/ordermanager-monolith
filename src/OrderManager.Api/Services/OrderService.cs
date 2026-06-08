using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class OrderService
{
    private readonly AppDbContext _context;
    private readonly InventoryServiceClient _inventoryClient;

    public OrderService(AppDbContext context, InventoryServiceClient inventoryClient)
    {
        _context = context;
        _inventoryClient = inventoryClient;
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

        // Track successful deductions so we can compensate on failure
        var deducted = new List<(int ProductId, int Quantity)>();

        try
        {
            foreach (var (productId, quantity) in items)
            {
                var product = await _context.Products.FindAsync(productId)
                    ?? throw new ArgumentException($"Product {productId} not found");

                // Deduct stock via the inventory microservice instead of direct DB access
                await _inventoryClient.DeductStockAsync(productId, quantity);
                deducted.Add((productId, quantity));

                order.Items.Add(new OrderItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }

            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }
        catch
        {
            // Compensate: restock any items that were already deducted
            foreach (var (productId, quantity) in deducted)
            {
                try
                {
                    await _inventoryClient.RestockAsync(productId, quantity);
                }
                catch
                {
                    // Best-effort compensation — log failures in production
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
