using Inventory.Api.Models;

namespace Inventory.Api.Data;

public static class SeedData
{
    public static void Initialize(InventoryDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.InventoryItems.Any()) return;

        var inventoryItems = new[]
        {
            new InventoryItem { ProductId = 1, QuantityOnHand = 50, ReorderLevel = 10, WarehouseLocation = "A-01" },
            new InventoryItem { ProductId = 2, QuantityOnHand = 100, ReorderLevel = 10, WarehouseLocation = "A-02" },
            new InventoryItem { ProductId = 3, QuantityOnHand = 150, ReorderLevel = 10, WarehouseLocation = "A-03" },
            new InventoryItem { ProductId = 4, QuantityOnHand = 200, ReorderLevel = 10, WarehouseLocation = "A-04" },
            new InventoryItem { ProductId = 5, QuantityOnHand = 250, ReorderLevel = 10, WarehouseLocation = "A-05" },
        };

        context.InventoryItems.AddRange(inventoryItems);
        context.SaveChanges();
    }
}
