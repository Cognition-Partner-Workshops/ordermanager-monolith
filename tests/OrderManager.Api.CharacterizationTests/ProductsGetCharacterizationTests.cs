using System.Net;

namespace OrderManager.Api.CharacterizationTests;

public class ProductsGetCharacterizationTests : IClassFixture<ProductsApiFactory>
{
    private readonly HttpClient _client;

    public ProductsGetCharacterizationTests(ProductsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsSeededProducts()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        await GoldenFileAssertions.AssertMatchesAsync(response, "products_list.json");
    }

    [Fact]
    public async Task GetProductById_ReturnsProduct()
    {
        var response = await _client.GetAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await GoldenFileAssertions.AssertMatchesAsync(response, "product_by_id.json");
    }

    [Fact]
    public async Task GetProductByUnknownId_ReturnsNotFoundProblemDetails()
    {
        var response = await _client.GetAsync("/api/products/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await GoldenFileAssertions.AssertMatchesAsync(response, "product_not_found.json");
    }

    [Fact]
    public async Task GetProductByInvalidId_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/products/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await GoldenFileAssertions.AssertMatchesAsync(response, "product_invalid_id.json");
    }

    [Fact]
    public async Task GetProductsByCategory_ReturnsMatchingProducts()
    {
        var response = await _client.GetAsync("/api/products/category/Widgets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await GoldenFileAssertions.AssertMatchesAsync(response, "products_by_category.json");
    }

    [Fact]
    public async Task GetProductsByUnknownCategory_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/products/category/Nonexistent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetProductsByLowercaseCategory_ReturnsActualCaseSensitiveResult()
    {
        var response = await _client.GetAsync("/api/products/category/widgets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }
}
