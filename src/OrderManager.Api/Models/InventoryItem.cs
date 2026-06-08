namespace OrderManager.Api.Models;

/// <summary>
/// DTO for inventory data returned by the inventory-service microservice.
/// No longer an EF entity — inventory is managed by the external inventory-service.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}
