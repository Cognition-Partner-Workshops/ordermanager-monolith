using Microsoft.AspNetCore.Mvc;
using OrderManager.Api.Clients;

namespace OrderManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerClient _customerClient;

    public CustomersController(ICustomerClient customerClient)
    {
        _customerClient = customerClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _customerClient.GetAllCustomersAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _customerClient.GetCustomerByIdAsync(id);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDto customer)
    {
        try
        {
            var created = await _customerClient.CreateCustomerAsync(customer);
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
