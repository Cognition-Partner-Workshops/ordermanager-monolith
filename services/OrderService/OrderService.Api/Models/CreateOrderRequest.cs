namespace OrderService.Api.Models;

public record CreateOrderRequest(int CustomerId, List<OrderItemRequest> Items);
public record OrderItemRequest(int ProductId, int Quantity);
public record UpdateStatusRequest(string Status);
