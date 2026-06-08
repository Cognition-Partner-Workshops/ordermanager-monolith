using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OrderManager.Api.Data;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

/// <summary>
/// Fake HTTP message handler that simulates inventory microservice responses for testing.
/// </summary>
public class FakeInventoryHandler : HttpMessageHandler
{
    private readonly AppDbContext _context;

    public FakeInventoryHandler(AppDbContext context)
    {
        _context = context;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? "";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // GET api/inventory/product/{id}/check
        if (path.Contains("/check") && request.Method == HttpMethod.Get)
        {
            var productId = ExtractProductId(path);
            var item = await _context.InventoryItems.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == productId, cancellationToken);
            if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ToDto(item), options: options) };
        }

        // POST api/inventory/product/{id}/deduct
        if (path.Contains("/deduct") && request.Method == HttpMethod.Post)
        {
            var productId = ExtractProductId(path);
            var item = await _context.InventoryItems.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == productId, cancellationToken);
            if (item is null) return new HttpResponseMessage(HttpStatusCode.NotFound);

            var body = await request.Content!.ReadFromJsonAsync<DeductRequest>(options, cancellationToken);
            if (item.QuantityOnHand < body!.Quantity) return new HttpResponseMessage(HttpStatusCode.BadRequest);

            item.QuantityOnHand -= body.Quantity;
            await _context.SaveChangesAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ToDto(item), options: options) };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static int ExtractProductId(string path)
    {
        // path like /api/inventory/product/1/check or /api/inventory/product/1/deduct
        var segments = path.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == "product" && i + 1 < segments.Length && int.TryParse(segments[i + 1], out var id))
                return id;
        }
        return 0;
    }

    private static InventoryItemDto ToDto(InventoryItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductName = item.Product?.Name ?? "",
        QuantityOnHand = item.QuantityOnHand,
        ReorderLevel = item.ReorderLevel,
        WarehouseLocation = item.WarehouseLocation,
        LastRestocked = item.LastRestocked
    };

    private record DeductRequest(int Quantity);
}

public class OrderServiceTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        SeedData.Initialize(context);
        return context;
    }

    private InventoryHttpClient CreateInventoryClient(AppDbContext context)
    {
        var handler = new FakeInventoryHandler(context);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://fake-inventory/") };
        return new InventoryHttpClient(httpClient);
    }

    [Fact]
    public async Task GetAllOrders_ReturnsEmptyList_WhenNoOrders()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(context);
        var service = new OrderService(context, inventoryClient);
        var orders = await service.GetAllOrdersAsync();
        Assert.Empty(orders);
    }

    [Fact]
    public async Task CreateOrder_DeductsInventory()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(context);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();
        var inventoryBefore = await context.InventoryItems.FirstAsync(i => i.ProductId == product.Id);
        var qtyBefore = inventoryBefore.QuantityOnHand;

        await service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 5) });

        var inventoryAfter = await context.InventoryItems.FirstAsync(i => i.ProductId == product.Id);
        Assert.Equal(qtyBefore - 5, inventoryAfter.QuantityOnHand);
    }

    [Fact]
    public async Task CreateOrder_ThrowsOnInsufficientStock()
    {
        using var context = CreateContext();
        var inventoryClient = CreateInventoryClient(context);
        var service = new OrderService(context, inventoryClient);
        var product = await context.Products.FirstAsync();
        var customer = await context.Customers.FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrderAsync(customer.Id, new List<(int, int)> { (product.Id, 99999) }));
    }
}
