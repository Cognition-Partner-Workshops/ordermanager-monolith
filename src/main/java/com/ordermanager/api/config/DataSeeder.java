package com.ordermanager.api.config;

import com.ordermanager.api.model.*;
import com.ordermanager.api.repository.*;
import org.springframework.boot.CommandLineRunner;
import org.springframework.stereotype.Component;
import java.math.BigDecimal;

@Component
public class DataSeeder implements CommandLineRunner {

    private final CustomerRepository customerRepository;
    private final ProductRepository productRepository;
    private final InventoryItemRepository inventoryItemRepository;

    public DataSeeder(CustomerRepository customerRepository,
                      ProductRepository productRepository,
                      InventoryItemRepository inventoryItemRepository) {
        this.customerRepository = customerRepository;
        this.productRepository = productRepository;
        this.inventoryItemRepository = inventoryItemRepository;
    }

    @Override
    public void run(String... args) {
        if (productRepository.count() > 0) return;

        Customer c1 = new Customer();
        c1.setName("Acme Corp"); c1.setEmail("orders@acme.com"); c1.setPhone("555-0100");
        c1.setAddress("123 Main St"); c1.setCity("Springfield"); c1.setState("IL"); c1.setZipCode("62701");

        Customer c2 = new Customer();
        c2.setName("Globex Inc"); c2.setEmail("purchasing@globex.com"); c2.setPhone("555-0200");
        c2.setAddress("456 Oak Ave"); c2.setCity("Shelbyville"); c2.setState("IL"); c2.setZipCode("62565");

        Customer c3 = new Customer();
        c3.setName("Initech LLC"); c3.setEmail("supplies@initech.com"); c3.setPhone("555-0300");
        c3.setAddress("789 Pine Rd"); c3.setCity("Capital City"); c3.setState("IL"); c3.setZipCode("62702");

        customerRepository.save(c1);
        customerRepository.save(c2);
        customerRepository.save(c3);

        Product p1 = createProduct("Widget A", "Standard widget", "Widgets", "9.99", "WGT-001");
        Product p2 = createProduct("Widget B", "Premium widget", "Widgets", "19.99", "WGT-002");
        Product p3 = createProduct("Gadget X", "Basic gadget", "Gadgets", "29.99", "GDG-001");
        Product p4 = createProduct("Gadget Y", "Advanced gadget", "Gadgets", "49.99", "GDG-002");
        Product p5 = createProduct("Thingamajig", "Multi-purpose thingamajig", "Misc", "14.99", "THG-001");

        Product[] products = { p1, p2, p3, p4, p5 };
        for (int i = 0; i < products.length; i++) {
            InventoryItem inv = new InventoryItem();
            inv.setProductId(products[i].getId());
            inv.setQuantityOnHand((i + 1) * 50);
            inv.setReorderLevel(10);
            inv.setWarehouseLocation("A-" + String.format("%02d", i + 1));
            inventoryItemRepository.save(inv);
        }
    }

    private Product createProduct(String name, String description, String category, String price, String sku) {
        Product p = new Product();
        p.setName(name);
        p.setDescription(description);
        p.setCategory(category);
        p.setPrice(new BigDecimal(price));
        p.setSku(sku);
        return productRepository.save(p);
    }
}
