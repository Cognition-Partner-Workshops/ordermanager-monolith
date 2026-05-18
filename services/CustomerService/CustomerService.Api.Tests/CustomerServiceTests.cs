using Microsoft.EntityFrameworkCore;
using CustomerService.Api.Data;
using CustomerService.Api.Models;
using CustomerService.Api.Services;

namespace CustomerService.Api.Tests;

public class CustomerServiceTests : IDisposable
{
    private readonly CustomerDbContext _context;
    private readonly CustomerServiceImpl _service;

    public CustomerServiceTests()
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CustomerDbContext(options);
        _service = new CustomerServiceImpl(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllCustomersAsync_ReturnsEmptyList_WhenNoCustomers()
    {
        var result = await _service.GetAllCustomersAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllCustomersAsync_ReturnsAllCustomers()
    {
        _context.Customers.AddRange(
            new Customer { Name = "Test Corp", Email = "test@corp.com", Phone = "555-0001" },
            new Customer { Name = "Other Inc", Email = "other@inc.com", Phone = "555-0002" }
        );
        await _context.SaveChangesAsync();

        var result = await _service.GetAllCustomersAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ReturnsCustomer_WhenExists()
    {
        var customer = new Customer { Name = "Acme Corp", Email = "orders@acme.com", Phone = "555-0100" };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var result = await _service.GetCustomerByIdAsync(customer.Id);
        Assert.NotNull(result);
        Assert.Equal("Acme Corp", result.Name);
        Assert.Equal("orders@acme.com", result.Email);
    }

    [Fact]
    public async Task GetCustomerByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetCustomerByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateCustomerAsync_AddsCustomerToDatabase()
    {
        var customer = new Customer
        {
            Name = "New Corp",
            Email = "new@corp.com",
            Phone = "555-9999",
            Address = "100 New St",
            City = "Newtown",
            State = "NY",
            ZipCode = "10001"
        };

        var result = await _service.CreateCustomerAsync(customer);

        Assert.True(result.Id > 0);
        Assert.Equal("New Corp", result.Name);

        var stored = await _context.Customers.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("new@corp.com", stored.Email);
    }

    [Fact]
    public async Task CreateCustomerAsync_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var customer = new Customer { Name = "Time Corp", Email = "time@corp.com", Phone = "555-0000" };

        var result = await _service.CreateCustomerAsync(customer);

        Assert.True(result.CreatedAt >= before.AddSeconds(-1));
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }
}
