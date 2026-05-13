package com.ordermanager.api.controller;

import com.ordermanager.api.service.InventoryService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/inventory")
public class InventoryController {

    private final InventoryService inventoryService;

    public InventoryController(InventoryService inventoryService) {
        this.inventoryService = inventoryService;
    }

    @GetMapping
    public ResponseEntity<?> getAll() {
        return ResponseEntity.ok(inventoryService.getAllInventory());
    }

    @GetMapping("/product/{productId}")
    public ResponseEntity<?> getByProduct(@PathVariable int productId) {
        return inventoryService.getInventoryByProductId(productId)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @PostMapping("/product/{productId}/restock")
    public ResponseEntity<?> restock(@PathVariable int productId, @Valid @RequestBody RestockRequest request) {
        return ResponseEntity.ok(inventoryService.restock(productId, request.quantity()));
    }

    @GetMapping("/low-stock")
    public ResponseEntity<?> getLowStock() {
        return ResponseEntity.ok(inventoryService.getLowStockItems());
    }

    public record RestockRequest(
            @Min(value = 1, message = "Restock quantity must be at least 1") int quantity) {}
}
