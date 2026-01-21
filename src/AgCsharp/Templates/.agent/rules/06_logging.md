# Logging Standards

## Serilog Configuration

### Setup in Program.cs

```csharp
// Using Serilog.AspNetCore
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.Seq("http://localhost:5341") // For structured log viewing
    .CreateLogger();

try
{
    Log.Information("Starting application");
    
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    
    // ... rest of setup
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### Configuration via appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"]
  }
}
```

## Log Levels

### When to Use Each Level

```csharp
// TRACE - Very detailed diagnostics (rarely used in production)
_logger.LogTrace("Entering method with parameter {Param}", param);

// DEBUG - Debugging information
_logger.LogDebug("Cache key {CacheKey} generated for request", cacheKey);

// INFORMATION - Normal operational events
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}", 
    order.Id, order.CustomerId);

// WARNING - Unexpected but handled situations
_logger.LogWarning("Retry attempt {Attempt} for external service call", attempt);

// ERROR - Errors that prevent operation completion
_logger.LogError(exception, "Failed to process order {OrderId}", orderId);

// CRITICAL - System-level failures
_logger.LogCritical(exception, "Database connection lost. Application shutting down");
```

## Correlation IDs

### Request Correlation Middleware

```csharp
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Add to log context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var id))
            return id.ToString();

        return Guid.NewGuid().ToString("N")[..8];
    }
}

// Register before other middleware
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Structured Logging Best Practices

### Use Message Templates (Not String Interpolation)

```csharp
// ✅ Good - structured, searchable
_logger.LogInformation(
    "Processing order {OrderId} for customer {CustomerId} with total {Total:C}",
    order.Id, 
    order.CustomerId, 
    order.Total);

// ❌ Bad - loses structure
_logger.LogInformation(
    $"Processing order {order.Id} for customer {order.CustomerId}");
```

### Add Context with Scopes

```csharp
public async Task ProcessOrderAsync(Order order)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = order.Id,
        ["CustomerId"] = order.CustomerId,
        ["Operation"] = nameof(ProcessOrderAsync)
    });

    _logger.LogInformation("Starting order processing");
    
    // All logs within this scope include OrderId, CustomerId, Operation
    await ValidateOrder(order);
    await ChargePayment(order);
    await SendConfirmation(order);

    _logger.LogInformation("Order processing completed");
}
```

### Log Object Properties

```csharp
// Use @ for destructuring objects
_logger.LogInformation("Order created: {@Order}", order);

// Use $ for ToString() representation
_logger.LogInformation("Order created: {$Order}", order);

// Custom destructuring policy in Serilog
.Destructure.ByTransforming<Customer>(c => new 
{ 
    c.Id, 
    c.Name, 
    Email = "***" // Don't log PII
})
```

## Request Logging

### HTTP Request Logging Middleware

```csharp
// Using Serilog.AspNetCore
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = 
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
        diagnosticContext.Set("UserId", httpContext.User?.Identity?.Name);
    };
    
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : LogEventLevel.Information;
});
```

## Performance Logging

```csharp
public class PerformanceLoggingBehavior<TRequest, TResponse>(
    ILogger<PerformanceLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Stopwatch _timer = new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _timer.Start();
        var response = await next();
        _timer.Stop();

        var elapsed = _timer.ElapsedMilliseconds;

        if (elapsed > 500) // Warn for slow operations
        {
            logger.LogWarning(
                "Long running request: {RequestName} ({ElapsedMs}ms) {@Request}",
                requestName,
                elapsed,
                request);
        }
        else
        {
            logger.LogDebug(
                "Request {RequestName} completed in {ElapsedMs}ms",
                requestName,
                elapsed);
        }

        return response;
    }
}
```

## Sensitive Data Protection

```csharp
// Never log:
// - Passwords
// - API keys/tokens
// - Credit card numbers
// - Personal identification numbers
// - Full email addresses (mask: j***@example.com)

// Create extension method for masking
public static class LoggingExtensions
{
    public static string MaskEmail(this string email)
    {
        if (string.IsNullOrEmpty(email)) return email;
        var parts = email.Split('@');
        if (parts.Length != 2) return "***";
        return $"{parts[0][0]}***@{parts[1]}";
    }

    public static string MaskCreditCard(this string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return "****";
        return $"****-****-****-{cardNumber[^4..]}";
    }
}

// Usage
_logger.LogInformation(
    "Payment processed for card {CardNumber}", 
    cardNumber.MaskCreditCard());
```

## Log Aggregation Setup

### Configure for Cloud Providers

```csharp
// Azure Application Insights
.WriteTo.ApplicationInsights(
    configuration["ApplicationInsights:ConnectionString"],
    TelemetryConverter.Traces)

// AWS CloudWatch
.WriteTo.AmazonCloudWatch(
    logGroup: "my-app",
    logStreamPrefix: Environment.MachineName,
    region: RegionEndpoint.USEast1)

// Elasticsearch
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(
    new Uri("http://localhost:9200"))
{
    IndexFormat = "myapp-logs-{0:yyyy.MM.dd}",
    AutoRegisterTemplate = true
})
```
