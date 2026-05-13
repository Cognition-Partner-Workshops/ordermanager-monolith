package com.ordermanager.api.service;

import com.ordermanager.api.model.*;
import com.ordermanager.api.repository.*;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.math.BigDecimal;
import java.util.List;
import java.util.Optional;

@Service
@Transactional(readOnly = true)
public class OrderService {

    private final OrderRepository orderRepository;
    private final CustomerRepository customerRepository;
    private final ProductRepository productRepository;
    private final InventoryItemRepository inventoryItemRepository;

    public OrderService(OrderRepository orderRepository,
                        CustomerRepository customerRepository,
                        ProductRepository productRepository,
                        InventoryItemRepository inventoryItemRepository) {
        this.orderRepository = orderRepository;
        this.customerRepository = customerRepository;
        this.productRepository = productRepository;
        this.inventoryItemRepository = inventoryItemRepository;
    }

    public List<Order> getAllOrders() {
        return orderRepository.findAllByOrderByOrderDateDesc();
    }

    public Optional<Order> getOrderById(int id) {
        return orderRepository.findById(id);
    }

    @Transactional
    public Order createOrder(int customerId, List<OrderItemRequest> items) {
        Customer customer = customerRepository.findById(customerId)
                .orElseThrow(() -> new IllegalArgumentException("Customer " + customerId + " not found"));

        Order order = new Order();
        order.setCustomerId(customerId);
        order.setShippingAddress(customer.getAddress() + ", " + customer.getCity() + ", "
                + customer.getState() + " " + customer.getZipCode());

        for (OrderItemRequest req : items) {
            Product product = productRepository.findById(req.productId())
                    .orElseThrow(() -> new IllegalArgumentException("Product " + req.productId() + " not found"));

            InventoryItem inventory = inventoryItemRepository.findByProductId(req.productId())
                    .orElseThrow(() -> new IllegalStateException("No inventory record for product " + req.productId()));

            if (inventory.getQuantityOnHand() < req.quantity()) {
                throw new IllegalStateException("Insufficient stock for " + product.getName()
                        + ". Available: " + inventory.getQuantityOnHand());
            }

            inventory.setQuantityOnHand(inventory.getQuantityOnHand() - req.quantity());
            inventoryItemRepository.save(inventory);

            OrderItem orderItem = new OrderItem();
            orderItem.setProductId(req.productId());
            orderItem.setQuantity(req.quantity());
            orderItem.setUnitPrice(product.getPrice());
            order.getItems().add(orderItem);
        }

        BigDecimal total = order.getItems().stream()
                .map(i -> i.getUnitPrice().multiply(BigDecimal.valueOf(i.getQuantity())))
                .reduce(BigDecimal.ZERO, BigDecimal::add);
        order.setTotalAmount(total);

        return orderRepository.save(order);
    }

    @Transactional
    public Order updateOrderStatus(int orderId, String status) {
        Order order = orderRepository.findById(orderId)
                .orElseThrow(() -> new IllegalArgumentException("Order " + orderId + " not found"));
        order.setStatus(status);
        return orderRepository.save(order);
    }

    public record OrderItemRequest(int productId, int quantity) {}
}
