using Microsoft.EntityFrameworkCore;
using OrderManager.Api.Data;
using OrderManager.Api.Models;

namespace OrderManager.Api.Services;

public class OrderService
{
    private readonly AppDbContext _context;
    private readonly IInventoryClient _inventory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext context, IInventoryClient inventory, ILogger<OrderService> logger)
    {
        _context = context;
        _inventory = inventory;
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

        var products = new Dictionary<int, Product>();
        foreach (var (productId, quantity) in items)
        {
            products[productId] = await _context.Products.FindAsync(productId)
                ?? throw new ArgumentException($"Product {productId} not found");

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = products[productId].Price
            });
        }

        // Validate the whole order against the inventory-service before deducting anything.
        var availability = await _inventory.CheckAvailabilityAsync(items.Select(i => new StockCheckLine(i.ProductId, i.Quantity)));
        var shortfall = availability.Items.FirstOrDefault(r => !r.Sufficient);
        if (shortfall is not null)
        {
            var name = products.TryGetValue(shortfall.ProductId, out var p) ? p.Name : $"product {shortfall.ProductId}";
            throw new InvalidOperationException($"Insufficient stock for {name}. Available: {shortfall.Available}");
        }

        var deducted = new List<(int ProductId, int Quantity)>();
        try
        {
            foreach (var (productId, quantity) in items)
            {
                await _inventory.DeductAsync(productId, quantity);
                deducted.Add((productId, quantity));
            }

            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }
        catch
        {
            await CompensateAsync(deducted);
            throw;
        }
    }

    private async Task CompensateAsync(List<(int ProductId, int Quantity)> deducted)
    {
        foreach (var (productId, quantity) in deducted)
        {
            try
            {
                await _inventory.RestockAsync(productId, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore {Quantity} units of product {ProductId} after order failure", quantity, productId);
            }
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
