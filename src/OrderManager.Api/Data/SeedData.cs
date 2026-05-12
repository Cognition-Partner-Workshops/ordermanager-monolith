using OrderManager.Api.Models;

namespace OrderManager.Api.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Products.Any()) return;

        var customers = new[]
        {
            new Customer { Name = "Acme Corp", Email = "orders@acme.com", Phone = "555-0100", Address = "123 Main St", City = "Springfield", State = "IL", ZipCode = "62701" },
            new Customer { Name = "Globex Inc", Email = "purchasing@globex.com", Phone = "555-0200", Address = "456 Oak Ave", City = "Shelbyville", State = "IL", ZipCode = "62565" },
            new Customer { Name = "Initech LLC", Email = "supplies@initech.com", Phone = "555-0300", Address = "789 Pine Rd", City = "Capital City", State = "IL", ZipCode = "62702" },
        };
        context.Customers.AddRange(customers);

        var products = new[]
        {
            new Product { Name = "Widget A", Description = "Standard widget", Category = "Widgets", Price = 9.99m, Sku = "WGT-001" },
            new Product { Name = "Widget B", Description = "Premium widget", Category = "Widgets", Price = 19.99m, Sku = "WGT-002" },
            new Product { Name = "Gadget X", Description = "Basic gadget", Category = "Gadgets", Price = 29.99m, Sku = "GDG-001" },
            new Product { Name = "Gadget Y", Description = "Advanced gadget", Category = "Gadgets", Price = 49.99m, Sku = "GDG-002" },
            new Product { Name = "Thingamajig", Description = "Multi-purpose thingamajig", Category = "Misc", Price = 14.99m, Sku = "THG-001" },
        };
        context.Products.AddRange(products);
        context.SaveChanges();

        var inventoryItems = products.Select((p, i) => new InventoryItem
        {
            ProductId = p.Id,
            QuantityOnHand = (i + 1) * 50,
            ReorderLevel = 10,
            WarehouseLocation = $"A-{i + 1:D2}"
        }).ToArray();
        context.InventoryItems.AddRange(inventoryItems);
        context.SaveChanges();

        var projects = new[]
        {
            new Project { Name = "Website Redesign", Description = "Complete redesign of corporate website", CustomerId = customers[0].Id, StartDate = new DateTime(2024, 1, 15), Status = "Active" },
            new Project { Name = "ERP Integration", Description = "Integrate with enterprise resource planning system", CustomerId = customers[1].Id, StartDate = new DateTime(2024, 3, 1), Status = "Active" },
            new Project { Name = "Mobile App", Description = "Develop mobile companion application", CustomerId = customers[2].Id, StartDate = new DateTime(2023, 6, 1), Status = "Completed" },
        };
        context.Projects.AddRange(projects);
        context.SaveChanges();
    }
}
