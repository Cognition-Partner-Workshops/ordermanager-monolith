using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Clients;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductClient _productClient;

    public ProductsController(IProductClient productClient)
    {
        _productClient = productClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _productClient.GetAllProductsAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productClient.GetProductByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetByCategory(string category) =>
        Ok(await _productClient.GetProductsByCategoryAsync(category));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductDto product)
    {
        try
        {
            var created = await _productClient.CreateProductAsync(product);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
