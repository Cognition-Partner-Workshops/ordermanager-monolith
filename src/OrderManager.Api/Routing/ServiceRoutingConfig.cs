namespace OrderManager.Api.Routing;

public class ServiceRoutingConfig
{
    public ServiceRouteMode Orders { get; set; } = ServiceRouteMode.Monolith;
    public ServiceRouteMode Products { get; set; } = ServiceRouteMode.Monolith;
    public ServiceRouteMode Customers { get; set; } = ServiceRouteMode.Monolith;
    public ServiceRouteMode Inventory { get; set; } = ServiceRouteMode.Monolith;
}

public enum ServiceRouteMode
{
    Monolith,
    Microservice
}

public class MicroserviceEndpoints
{
    public string OrdersUrl { get; set; } = string.Empty;
    public string ProductsUrl { get; set; } = string.Empty;
    public string CustomersUrl { get; set; } = string.Empty;
    public string InventoryUrl { get; set; } = string.Empty;
}
