using System.Net;
using System.Net.Http.Json;

namespace OrderManager.Api.CharacterizationTests;

public class ProductDuplicateSkuCharacterizationTests : IClassFixture<ProductsApiFactory>
{
    private readonly HttpClient _client;

    public ProductDuplicateSkuCharacterizationTests(ProductsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProductWithDuplicateSku_ReturnsInternalServerError()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            name = "Duplicate SKU",
            description = "Duplicate SKU characterization",
            category = "Widgets",
            price = 12.34m,
            sku = "WGT-001"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.NotEmpty(await response.Content.ReadAsStringAsync());
    }
}
