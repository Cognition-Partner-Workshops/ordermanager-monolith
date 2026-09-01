using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OrderManager.Api.CharacterizationTests;

public class ProductCreateCharacterizationTests : IClassFixture<ProductsApiFactory>
{
    private readonly HttpClient _client;

    public ProductCreateCharacterizationTests(ProductsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreatedProductAndPersistsIt()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            name = "Test Product",
            description = "Characterization product",
            category = "Widgets",
            price = 12.34m,
            sku = "TST-001"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!;
        var locationPath = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        Assert.Equal("/api/Products/6", locationPath);

        var body = await response.Content.ReadAsStringAsync();
        var createdId = JsonNode.Parse(body)!["id"]!.GetValue<int>();
        await GoldenFileAssertions.AssertMatchesAsync(response, "product_created.json");

        var getResponse = await _client.GetAsync($"/api/products/{createdId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        await GoldenFileAssertions.AssertMatchesAsync(getResponse, "product_created_persisted.json");
    }
}
