using OrderManager.Api.Interfaces;
using OrderManager.Api.Proxies;
using OrderManager.Api.Services;

namespace OrderManager.Api.Routing;

public static class ServiceRoutingExtensions
{
    public static IServiceCollection AddRoutedServices(this IServiceCollection services, IConfiguration configuration)
    {
        var routingConfig = configuration.GetSection("ServiceRouting").Get<ServiceRoutingConfig>() ?? new ServiceRoutingConfig();
        var endpoints = configuration.GetSection("MicroserviceEndpoints").Get<MicroserviceEndpoints>() ?? new MicroserviceEndpoints();

        if (routingConfig.Customers == ServiceRouteMode.Microservice && !string.IsNullOrEmpty(endpoints.CustomersUrl))
        {
            services.AddHttpClient<ICustomerService, CustomerServiceProxy>(client =>
                client.BaseAddress = new Uri(endpoints.CustomersUrl));
        }
        else
        {
            services.AddScoped<ICustomerService, CustomerService>();
        }

        if (routingConfig.Products == ServiceRouteMode.Microservice && !string.IsNullOrEmpty(endpoints.ProductsUrl))
        {
            services.AddHttpClient<IProductService, ProductServiceProxy>(client =>
                client.BaseAddress = new Uri(endpoints.ProductsUrl));
        }
        else
        {
            services.AddScoped<IProductService, ProductService>();
        }

        if (routingConfig.Inventory == ServiceRouteMode.Microservice && !string.IsNullOrEmpty(endpoints.InventoryUrl))
        {
            services.AddHttpClient<IInventoryService, InventoryServiceProxy>(client =>
                client.BaseAddress = new Uri(endpoints.InventoryUrl));
        }
        else
        {
            services.AddScoped<IInventoryService, InventoryService>();
        }

        if (routingConfig.Orders == ServiceRouteMode.Microservice && !string.IsNullOrEmpty(endpoints.OrdersUrl))
        {
            services.AddHttpClient<IOrderService, OrderServiceProxy>(client =>
                client.BaseAddress = new Uri(endpoints.OrdersUrl));
        }
        else
        {
            services.AddScoped<IOrderService, OrderService>();
        }

        return services;
    }
}
