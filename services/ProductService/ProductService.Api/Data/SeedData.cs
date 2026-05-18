using ProductService.Api.Models;

namespace ProductService.Api.Data;

public static class SeedData
{
    public static void Initialize(ProductDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Products.Any()) return;

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
    }
}
