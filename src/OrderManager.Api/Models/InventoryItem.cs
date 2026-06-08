namespace OrderManager.Api.Models;

/// <summary>
/// DTO for inventory data returned by the inventory microservice.
/// No longer stored in the monolith's database.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; }
}
