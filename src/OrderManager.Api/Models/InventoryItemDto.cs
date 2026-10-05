namespace OrderManager.Api.Models;

// Read model returned by the inventory-service HTTP API. Inventory is no longer stored in the monolith database.
public class InventoryItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsLowStock { get; set; }
}

public record StockCheckLine(int ProductId, int Quantity);
public record StockCheckRequest(List<StockCheckLine> Items);
public record StockCheckResult(int ProductId, int Requested, int Available, bool Sufficient);
public record StockCheckResponse(bool AllAvailable, List<StockCheckResult> Items);
