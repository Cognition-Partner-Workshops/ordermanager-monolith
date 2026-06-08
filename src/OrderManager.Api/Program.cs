using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OrderManager.Api.Data;
using OrderManager.Api.Services;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ordermanager.db"));

// Register the inventory-service HTTP client with Polly resilience (circuit breaker + retry)
var inventoryServiceUrl = builder.Configuration["InventoryService:BaseUrl"] ?? "http://localhost:5062";
builder.Services.AddHttpClient<InventoryHttpClient>(client =>
{
    client.BaseAddress = new Uri(inventoryServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddResilienceHandler("inventory-resilience", builder =>
{
    // Retry: up to 3 attempts with exponential backoff
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromMilliseconds(500),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                            || r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
    });

    // Circuit breaker: open after 5 failures, stay open for 30s
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(60),
        FailureRatio = 0.5,
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(r => r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                            || r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
    });

    // Timeout: 10s per individual attempt
    builder.AddTimeout(TimeSpan.FromSeconds(10));
});

builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CustomerService>();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    SeedData.Initialize(context);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
