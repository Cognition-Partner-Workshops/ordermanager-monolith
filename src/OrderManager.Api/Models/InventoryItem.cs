namespace OrderManager.Api.Models;

/// <summary>
/// DTO for deserializing inventory data from the inventory-service microservice.
/// No longer a local EF Core entity — inventory is managed by the inventory-service.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}
