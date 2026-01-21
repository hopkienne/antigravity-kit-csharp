# Skill: Generate Health Check

## When to Use
User requests to create health check endpoints for monitoring application and dependency health.

## Template

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("{name}", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/health");
```

## Example Output

### Complete Health Check Setup

```csharp
// Program.cs
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks()
    // Self check
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    
    // SQL Server
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("Default")!,
        healthQuery: "SELECT 1",
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "sql", "ready"])
    
    // Redis
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "ready"])
    
    // External API
    .AddUrlGroup(
        new Uri(builder.Configuration["ExternalServices:PaymentApi:HealthUrl"]!),
        name: "payment-api",
        failureStatus: HealthStatus.Degraded,
        tags: ["external", "ready"])
    
    // Custom check
    .AddCheck<StorageHealthCheck>("storage", tags: ["storage", "ready"])
    
    // RabbitMQ
    .AddRabbitMQ(
        rabbitConnectionString: builder.Configuration["RabbitMQ:ConnectionString"]!,
        name: "rabbitmq",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["messaging", "ready"]);

var app = builder.Build();

// Map health endpoints
// Liveness probe - is the app running?
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteMinimalResponse
});

// Readiness probe - can the app handle requests?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Full health check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

static Task WriteMinimalResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(
        $"{{\"status\":\"{report.Status}\"}}");
}
```

### Custom Health Check

```csharp
namespace MyApp.Infrastructure.HealthChecks;

public class StorageHealthCheck(
    IStorageService storageService,
    ILogger<StorageHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canWrite = await storageService.CanWriteAsync(cancellationToken);
            var canRead = await storageService.CanReadAsync(cancellationToken);
            
            if (canWrite && canRead)
            {
                return HealthCheckResult.Healthy("Storage is accessible");
            }
            
            var data = new Dictionary<string, object>
            {
                { "CanWrite", canWrite },
                { "CanRead", canRead }
            };
            
            return HealthCheckResult.Degraded(
                "Storage has limited functionality",
                data: data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Storage health check failed");
            
            return HealthCheckResult.Unhealthy(
                "Storage is not accessible",
                exception: ex);
        }
    }
}
```

### Database Health Check with Custom Query

```csharp
public class DatabaseHealthCheck(
    ApplicationDbContext context,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Check database connection
            await context.Database.CanConnectAsync(cancellationToken);
            
            // Check if migrations are applied
            var pendingMigrations = await context.Database
                .GetPendingMigrationsAsync(cancellationToken);
            
            stopwatch.Stop();
            
            var data = new Dictionary<string, object>
            {
                { "ResponseTimeMs", stopwatch.ElapsedMilliseconds },
                { "PendingMigrations", pendingMigrations.Count() }
            };
            
            if (pendingMigrations.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Database has {pendingMigrations.Count()} pending migrations",
                    data: data);
            }
            
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database response time is slow: {stopwatch.ElapsedMilliseconds}ms",
                    data: data);
            }
            
            return HealthCheckResult.Healthy(
                $"Database is healthy (response: {stopwatch.ElapsedMilliseconds}ms)",
                data: data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
```

### External Service Health Check

```csharp
public class PaymentServiceHealthCheck(
    HttpClient httpClient,
    ILogger<PaymentServiceHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            var response = await httpClient.GetAsync(
                "/health",
                cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Payment service is available");
            }
            
            return HealthCheckResult.Degraded(
                $"Payment service returned {response.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Degraded("Payment service timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Payment service health check failed");
            return HealthCheckResult.Unhealthy("Payment service is unavailable", ex);
        }
    }
}
```

### Memory Health Check

```csharp
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _threshold;
    
    public MemoryHealthCheck(long thresholdInBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _threshold = thresholdInBytes;
    }
    
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        
        var data = new Dictionary<string, object>
        {
            { "AllocatedBytes", allocated },
            { "AllocatedMB", allocated / 1024 / 1024 },
            { "ThresholdMB", _threshold / 1024 / 1024 },
            { "Gen0Collections", GC.CollectionCount(0) },
            { "Gen1Collections", GC.CollectionCount(1) },
            { "Gen2Collections", GC.CollectionCount(2) }
        };
        
        if (allocated >= _threshold)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Memory usage ({allocated / 1024 / 1024}MB) exceeds threshold",
                data: data));
        }
        
        if (allocated >= _threshold * 0.8)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage ({allocated / 1024 / 1024}MB) is high",
                data: data));
        }
        
        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage is within limits ({allocated / 1024 / 1024}MB)",
            data: data));
    }
}
```

### DI Registration

```csharp
// In DependencyInjection.cs
public static IServiceCollection AddHealthChecks(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["db", "ready"])
        .AddCheck<StorageHealthCheck>("storage", tags: ["storage", "ready"])
        .AddCheck<MemoryHealthCheck>("memory", tags: ["system"])
        .AddCheck<PaymentServiceHealthCheck>("payment", tags: ["external"]);
    
    // Register HTTP client for external checks
    services.AddHttpClient<PaymentServiceHealthCheck>(client =>
    {
        client.BaseAddress = new Uri(configuration["ExternalServices:PaymentApi:Url"]!);
        client.Timeout = TimeSpan.FromSeconds(5);
    });
    
    return services;
}
```

### Health Check Response Example

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Database is healthy (response: 15ms)",
      "duration": "00:00:00.0150000",
      "data": {
        "ResponseTimeMs": 15,
        "PendingMigrations": 0
      }
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.0050000"
    },
    "storage": {
      "status": "Degraded",
      "description": "Storage has limited functionality",
      "duration": "00:00:00.0300000",
      "data": {
        "CanWrite": true,
        "CanRead": false
      }
    }
  }
}
```

## Guidelines

1. **Liveness vs Readiness** - Separate concerns for orchestrators
2. **Tags** - Categorize checks for selective endpoints
3. **Timeouts** - Set reasonable timeouts for external checks
4. **Degraded status** - Use for non-critical issues
5. **Data dictionary** - Include diagnostic information
6. **Log failures** - Help with troubleshooting
7. **Caching** - Consider caching expensive checks
