package com.ordermanager.api.controller;

import com.ordermanager.api.service.OrderService;
import com.ordermanager.api.service.OrderService.OrderItemRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotBlank;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import java.net.URI;
import java.util.List;

@RestController
@RequestMapping("/api/orders")
public class OrdersController {

    private final OrderService orderService;

    public OrdersController(OrderService orderService) {
        this.orderService = orderService;
    }

    @GetMapping
    public ResponseEntity<?> getAll() {
        return ResponseEntity.ok(orderService.getAllOrders());
    }

    @GetMapping("/{id}")
    public ResponseEntity<?> getById(@PathVariable int id) {
        return orderService.getOrderById(id)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PostMapping
    public ResponseEntity<?> create(@Valid @RequestBody CreateOrderRequest request) {
        List<OrderItemRequest> items = request.items().stream()
                .map(i -> new OrderItemRequest(i.productId(), i.quantity()))
                .toList();
        var order = orderService.createOrder(request.customerId(), items);
        return ResponseEntity.created(URI.create("/api/orders/" + order.getId())).body(order);
    }

    @PatchMapping("/{id}/status")
    public ResponseEntity<?> updateStatus(@PathVariable int id, @Valid @RequestBody UpdateStatusRequest request) {
        return ResponseEntity.ok(orderService.updateOrderStatus(id, request.status()));
    }

    public record CreateOrderRequest(
            @Min(value = 1, message = "Customer ID must be positive") int customerId,
            @NotEmpty(message = "Order must have at least one item") List<@Valid OrderItemDto> items) {}
    public record OrderItemDto(
            @Min(value = 1, message = "Product ID must be positive") int productId,
            @Min(value = 1, message = "Quantity must be at least 1") int quantity) {}
    public record UpdateStatusRequest(
            @NotBlank(message = "Status is required") String status) {}
}
