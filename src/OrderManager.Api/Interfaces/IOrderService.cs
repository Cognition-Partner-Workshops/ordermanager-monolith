using OrderManager.Api.Models;

namespace OrderManager.Api.Interfaces;

public interface IOrderService
{
    Task<List<Order>> GetAllOrdersAsync();
    Task<Order?> GetOrderByIdAsync(int id);
    Task<Order> CreateOrderAsync(int customerId, List<(int ProductId, int Quantity)> items);
    Task<Order> UpdateOrderStatusAsync(int orderId, string status);
}
