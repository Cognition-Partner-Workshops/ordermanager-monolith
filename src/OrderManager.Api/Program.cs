using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using OrderManager.Api.Data;
using OrderManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var logger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Polly");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=ordermanager.db"));

builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CustomerService>();

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, delay, attempt, _) =>
        {
            logger.LogWarning("Retry {Attempt} for inventory-service after {Delay}s — {Reason}", attempt, delay.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
        });

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (outcome, duration) =>
        {
            logger.LogWarning("Circuit OPEN for inventory-service — breaking for {Duration}s. Reason: {Reason}", duration.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
        },
        onReset: () =>
        {
            logger.LogInformation("Circuit CLOSED for inventory-service — calls resuming");
        },
        onHalfOpen: () =>
        {
            logger.LogInformation("Circuit HALF-OPEN for inventory-service — testing next call");
        });

builder.Services.AddHttpClient<InventoryHttpClient>(client =>
{
    var baseUrl = builder.Configuration["InventoryService:BaseUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);

builder.Services.AddHealthChecks();

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
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");
app.Run();
