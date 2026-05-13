package com.ordermanager.api.repository;

import com.ordermanager.api.model.InventoryItem;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.List;
import java.util.Optional;

public interface InventoryItemRepository extends JpaRepository<InventoryItem, Integer> {
    Optional<InventoryItem> findByProductId(Integer productId);
    List<InventoryItem> findByQuantityOnHandLessThanEqual(int reorderLevel);
}
