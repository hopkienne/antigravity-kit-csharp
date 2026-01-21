# Skill: Generate Middleware

## When to Use
User requests to create custom middleware for request/response processing, cross-cutting concerns.

## Template

```csharp
namespace {Namespace}.Api.Middleware;

public class {Name}Middleware(
    RequestDelegate next,
    ILogger<{Name}Middleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Before request processing
        
        await next(context);
        
        // After request processing
    }
}

public static class {Name}MiddlewareExtensions
{
    public static IApplicationBuilder Use{Name}(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<{Name}Middleware>();
    }
}
```

## Example Output

### Exception Handling Middleware

```csharp
namespace MyApp.Api.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problemDetails) = exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                CreateValidationProblemDetails(context, validationEx)),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path
                }),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    Title = "Unauthorized",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = "You are not authorized to access this resource",
                    Instance = context.Request.Path
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "An unexpected error occurred",
                    Instance = context.Request.Path
                })
        };

        logger.LogError(exception, 
            "Exception occurred processing {Method} {Path}: {Message}",
            context.Request.Method,
            context.Request.Path,
            exception.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext context,
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };
    }
}
```

### Correlation ID Middleware

```csharp
namespace MyApp.Api.Middleware;

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

        // Add to HttpContext items for access in application
        context.Items["CorrelationId"] = correlationId;

        // Add to log context (Serilog)
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId) 
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        return Guid.NewGuid().ToString("N")[..8];
    }
}
```

### Request Logging Middleware

```csharp
namespace MyApp.Api.Middleware;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Log request
        logger.LogInformation(
            "HTTP {Method} {Path} started",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            var level = context.Response.StatusCode >= 500 
                ? LogLevel.Error 
                : context.Response.StatusCode >= 400 
                    ? LogLevel.Warning 
                    : LogLevel.Information;

            logger.Log(level,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

### Rate Limiting Middleware (Custom)

```csharp
namespace MyApp.Api.Middleware;

public class RateLimitingMiddleware(
    RequestDelegate next,
    IDistributedCache cache,
    ILogger<RateLimitingMiddleware> logger)
{
    private const int MaxRequests = 100;
    private const int WindowSeconds = 60;

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var cacheKey = $"rate-limit:{clientId}";

        var currentCount = await GetRequestCountAsync(cacheKey);

        if (currentCount >= MaxRequests)
        {
            logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
            
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = WindowSeconds.ToString();
            
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Rate limit exceeded. Try again in {WindowSeconds} seconds."
            });
            
            return;
        }

        await IncrementRequestCountAsync(cacheKey);
        
        context.Response.Headers["X-RateLimit-Limit"] = MaxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = 
            (MaxRequests - currentCount - 1).ToString();

        await next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Use user ID if authenticated, otherwise IP address
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst("sub")?.Value 
                   ?? context.User.Identity.Name 
                   ?? context.Connection.RemoteIpAddress?.ToString() 
                   ?? "unknown";
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task<int> GetRequestCountAsync(string key)
    {
        var value = await cache.GetStringAsync(key);
        return int.TryParse(value, out var count) ? count : 0;
    }

    private async Task IncrementRequestCountAsync(string key)
    {
        var count = await GetRequestCountAsync(key) + 1;
        await cache.SetStringAsync(
            key,
            count.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(WindowSeconds)
            });
    }
}
```

### API Key Middleware

```csharp
namespace MyApp.Api.Middleware;

public class ApiKeyMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    private const string ApiKeyHeader = "X-API-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for health endpoints
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "API Key is missing"
            });
            return;
        }

        var validApiKey = configuration["ApiKey"];
        
        if (!string.Equals(extractedApiKey, validApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Invalid API Key"
            });
            return;
        }

        await next(context);
    }
}
```

### Registration in Program.cs

```csharp
// Order matters!
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

// Or using extension methods
app.UseCorrelationId();
app.UseRequestLogging();
app.UseExceptionHandling();
```

## Guidelines

1. **Order matters** - Register middleware in correct order
2. **Always call next()** - Unless short-circuiting intentionally
3. **Handle exceptions** - Wrap next() in try-catch if needed
4. **Use extension methods** - For clean registration
5. **Inject via constructor** - Use DI for dependencies
6. **Keep lightweight** - Don't do heavy processing
