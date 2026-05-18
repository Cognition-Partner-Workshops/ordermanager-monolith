using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=orders.db"));

builder.Services.AddScoped<IOrderManagementService, OrderManagementService>();

builder.Services.AddHttpClient("CustomerServiceClient", client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:CustomerServiceUrl"] ?? "http://localhost:5101"));

builder.Services.AddHttpClient("ProductServiceClient", client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:ProductServiceUrl"] ?? "http://localhost:5102"));

builder.Services.AddHttpClient("InventoryServiceClient", client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceEndpoints:InventoryServiceUrl"] ?? "http://localhost:5103"));

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    context.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();
app.Run();
