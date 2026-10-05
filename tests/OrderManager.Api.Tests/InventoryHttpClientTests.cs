using Xunit;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrderManager.Api.Models;
using OrderManager.Api.Services;

namespace OrderManager.Api.Tests;

public class InventoryHttpClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            return await _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
    };

    private static (InventoryHttpClient Client, StubHandler Handler) Create(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
    {
        var handler = new StubHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://inventory-service/") };
        return (new InventoryHttpClient(http, NullLogger<InventoryHttpClient>.Instance), handler);
    }

    [Fact]
    public async Task GetAll_CallsInventoryEndpoint_AndDeserializesItems()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(HttpStatusCode.OK, new[]
        {
            new { id = 1, productId = 1, productName = "Widget A", productSku = "WGT-001", quantityOnHand = 50, reorderLevel = 10, warehouseLocation = "A-01", isLowStock = false }
        })));

        var items = await client.GetAllAsync();

        Assert.Equal("http://inventory-service/api/inventory", handler.Requests.Single().RequestUri!.ToString());
        Assert.Single(items);
        Assert.Equal("Widget A", items[0].ProductName);
        Assert.Equal(50, items[0].QuantityOnHand);
    }

    [Fact]
    public async Task GetByProductId_ReturnsNullOn404()
    {
        var (client, _) = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await client.GetByProductIdAsync(42));
    }

    [Fact]
    public async Task Deduct_PostsQuantity_ToDeductEndpoint()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(HttpStatusCode.OK, new { productId = 2, quantityOnHand = 95 })));

        var result = await client.DeductAsync(2, 5);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://inventory-service/api/inventory/product/2/deduct", request.RequestUri!.ToString());
        Assert.Contains("\"quantity\":5", handler.Bodies.Single());
        Assert.Equal(95, result.QuantityOnHand);
    }

    [Fact]
    public async Task Deduct_MapsConflictToInvalidOperation_WithServiceMessage()
    {
        var (client, _) = Create(_ => Task.FromResult(Json(HttpStatusCode.Conflict, new { error = "Insufficient stock for product 2. Requested 500, available 100" })));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeductAsync(2, 500));
        Assert.Contains("Insufficient stock", ex.Message);
    }

    [Fact]
    public async Task Restock_MapsNotFoundToArgumentException()
    {
        var (client, _) = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RestockAsync(99, 5));
    }

    [Fact]
    public async Task CheckAvailability_PostsAllLines_AndReturnsResponse()
    {
        var (client, handler) = Create(_ => Task.FromResult(Json(HttpStatusCode.OK, new
        {
            allAvailable = false,
            items = new[] { new { productId = 1, requested = 5, available = 50, sufficient = true }, new { productId = 2, requested = 500, available = 100, sufficient = false } }
        })));

        var response = await client.CheckAvailabilityAsync(new[] { new StockCheckLine(1, 5), new StockCheckLine(2, 500) });

        Assert.Equal("http://inventory-service/api/inventory/check", handler.Requests.Single().RequestUri!.ToString());
        Assert.False(response.AllAvailable);
        Assert.Equal(2, response.Items.Count);
        Assert.False(response.Items[1].Sufficient);
    }

    [Fact]
    public async Task ConnectionFailure_IsWrappedAsUnavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));
        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(() => client.GetAllAsync());
    }

    [Fact]
    public async Task ServerError_IsWrappedAsUnavailable()
    {
        var (client, _) = Create(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        await Assert.ThrowsAsync<InventoryServiceUnavailableException>(() => client.GetLowStockAsync());
    }
}
