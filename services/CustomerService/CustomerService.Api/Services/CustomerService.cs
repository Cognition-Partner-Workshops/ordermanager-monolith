using Microsoft.EntityFrameworkCore;
using CustomerService.Api.Data;
using CustomerService.Api.Interfaces;
using CustomerService.Api.Models;

namespace CustomerService.Api.Services;

public class CustomerServiceImpl : ICustomerService
{
    private readonly CustomerDbContext _context;

    public CustomerServiceImpl(CustomerDbContext context)
    {
        _context = context;
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        return await _context.Customers.ToListAsync();
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }
}
