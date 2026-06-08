using System.Text.Json.Serialization;

namespace OrderManager.Api.Models;

public class InventoryItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }

    [JsonIgnore]
    public Product Product { get; set; } = null!;

    // Fields returned by the inventory microservice (not stored in the monolith DB)
    public string? ProductName { get; set; }
    public string? Sku { get; set; }

    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public string WarehouseLocation { get; set; } = string.Empty;
    public DateTime LastRestocked { get; set; } = DateTime.UtcNow;
}
