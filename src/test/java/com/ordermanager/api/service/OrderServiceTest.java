package com.ordermanager.api.service;

import com.ordermanager.api.model.*;
import com.ordermanager.api.repository.*;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import java.math.BigDecimal;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;

@DataJpaTest
@Import(OrderService.class)
class OrderServiceTest {

    @Autowired
    private OrderRepository orderRepository;
    @Autowired
    private CustomerRepository customerRepository;
    @Autowired
    private ProductRepository productRepository;
    @Autowired
    private InventoryItemRepository inventoryItemRepository;
    @Autowired
    private OrderService orderService;

    private Customer customer;
    private Product product;

    @BeforeEach
    void setUp() {
        customer = new Customer();
        customer.setName("Test Customer");
        customer.setEmail("test@example.com");
        customer.setAddress("123 Test St");
        customer.setCity("TestCity");
        customer.setState("TS");
        customer.setZipCode("00000");
        customer = customerRepository.save(customer);

        product = new Product();
        product.setName("Test Product");
        product.setSku("TST-001");
        product.setPrice(new BigDecimal("9.99"));
        product = productRepository.save(product);

        InventoryItem inventory = new InventoryItem();
        inventory.setProductId(product.getId());
        inventory.setQuantityOnHand(50);
        inventory.setReorderLevel(10);
        inventory.setWarehouseLocation("A-01");
        inventoryItemRepository.save(inventory);
    }

    @Test
    void getAllOrders_returnsEmptyList_whenNoOrders() {
        List<Order> orders = orderService.getAllOrders();
        assertTrue(orders.isEmpty());
    }

    @Test
    void createOrder_deductsInventory() {
        int qtyBefore = inventoryItemRepository.findByProductId(product.getId()).orElseThrow().getQuantityOnHand();

        orderService.createOrder(customer.getId(),
                List.of(new OrderService.OrderItemRequest(product.getId(), 5)));

        int qtyAfter = inventoryItemRepository.findByProductId(product.getId()).orElseThrow().getQuantityOnHand();
        assertEquals(qtyBefore - 5, qtyAfter);
    }

    @Test
    void createOrder_throwsOnInsufficientStock() {
        assertThrows(IllegalStateException.class,
                () -> orderService.createOrder(customer.getId(),
                        List.of(new OrderService.OrderItemRequest(product.getId(), 99999))));
    }
}
