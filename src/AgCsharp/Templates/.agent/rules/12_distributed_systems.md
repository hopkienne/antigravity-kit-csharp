# Distributed Systems & Resilience

## Polly Resilience Library

### Setup with .NET 8+ Resilience

```csharp
// Program.cs - Using Microsoft.Extensions.Http.Resilience
builder.Services.AddHttpClient<IExternalService, ExternalService>(client =>
{
    client.BaseAddress = new Uri(configuration["ExternalService:Url"]!);
})
.AddStandardResilienceHandler();

// Or configure custom resilience
builder.Services.AddHttpClient<IExternalService, ExternalService>()
.AddResilienceHandler("custom", builder =>
{
    builder
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = args => 
                ValueTask.FromResult(args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError)
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30)
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

### Classic Polly Configuration

```csharp
// Retry Policy
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff
        onRetry: (outcome, timespan, attempt, context) =>
        {
            _logger.LogWarning(
                "Retry {Attempt} after {Delay}s due to {Reason}",
                attempt,
                timespan.TotalSeconds,
                outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
        });

// Circuit Breaker Policy
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (outcome, breakDelay) =>
        {
            _logger.LogWarning("Circuit breaker opened for {BreakDelay}", breakDelay);
        },
        onReset: () =>
        {
            _logger.LogInformation("Circuit breaker reset");
        });

// Combine policies
var policyWrap = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
```

## Circuit Breaker Pattern

### States

```
┌─────────────────────────────────────────────────────────────────┐
│                    Circuit Breaker States                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────┐   Failure threshold   ┌────────┐                    │
│  │ CLOSED │ ─────────────────────►│  OPEN  │                    │
│  │        │                        │        │                    │
│  └────┬───┘                        └───┬────┘                    │
│       ▲                                │                         │
│       │        ┌────────────┐          │ After break duration   │
│       │        │ HALF-OPEN  │◄─────────┘                        │
│       │        │            │                                    │
│       │        └──────┬─────┘                                    │
│       │               │                                          │
│       │ Success       │ Failure                                  │
│       └───────────────┘                                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Implementation

```csharp
public class ResilientExternalService(
    HttpClient httpClient,
    ILogger<ResilientExternalService> logger)
{
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline = 
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .Handle<HttpRequestException>()
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 8,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
                    .Handle<HttpRequestException>()
            })
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await Pipeline.ExecuteAsync(
                async token => await httpClient.GetAsync(endpoint, token),
                ct);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning("Circuit is open, returning fallback");
            return default;
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning("Request timed out");
            return default;
        }
    }
}
```

## Retry Strategies

### Exponential Backoff with Jitter

```csharp
public static class RetryPolicies
{
    public static AsyncRetryPolicy<HttpResponseMessage> ExponentialBackoffWithJitter()
    {
        var jitter = new Random();
        
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt =>
                {
                    var exponentialDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    var jitterDelay = TimeSpan.FromMilliseconds(jitter.Next(0, 1000));
                    return exponentialDelay + jitterDelay;
                });
    }
}
```

## Timeout Strategies

```csharp
// Per-request timeout
var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
    seconds: 10,
    timeoutStrategy: TimeoutStrategy.Pessimistic,
    onTimeoutAsync: (context, timespan, task) =>
    {
        _logger.LogWarning("Request timed out after {Timespan}", timespan);
        return Task.CompletedTask;
    });

// Optimistic (cooperative) vs Pessimistic timeout
// Optimistic: Relies on CancellationToken - cleaner but requires support
// Pessimistic: Creates separate thread - works with any code but more overhead
```

## Bulkhead Pattern

Isolate resources to prevent failures from spreading:

```csharp
// Limit concurrent calls
var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(
    maxParallelization: 10,
    maxQueuingActions: 20,
    onBulkheadRejectedAsync: (context) =>
    {
        _logger.LogWarning("Bulkhead rejected request");
        return Task.CompletedTask;
    });

// Using SemaphoreSlim
public class ThrottledService(IExternalClient client)
{
    private readonly SemaphoreSlim _semaphore = new(10); // Max 10 concurrent

    public async Task<Result> ProcessAsync(Request request, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await client.CallAsync(request, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

## Fallback Pattern

```csharp
var fallbackPolicy = Policy<OrderDetails>
    .Handle<Exception>()
    .FallbackAsync(
        fallbackValue: OrderDetails.Empty,
        onFallbackAsync: (exception, context) =>
        {
            _logger.LogWarning(
                exception.Exception,
                "Falling back to default order details");
            return Task.CompletedTask;
        });

// With fallback action
var fallbackWithAction = Policy<OrderDetails>
    .Handle<Exception>()
    .FallbackAsync(
        fallbackAction: async ct => await _cache.GetCachedOrderAsync(),
        onFallbackAsync: (exception, context) =>
        {
            _logger.LogWarning("Using cached data as fallback");
            return Task.CompletedTask;
        });
```

## Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddSqlServer(
        connectionString: configuration.GetConnectionString("Default")!,
        name: "database",
        tags: ["db", "sql"])
    .AddRedis(
        redisConnectionString: configuration.GetConnectionString("Redis")!,
        name: "redis",
        tags: ["cache"])
    .AddUrlGroup(
        new Uri(configuration["ExternalService:HealthUrl"]!),
        name: "external-service",
        tags: ["external"]);

// Map endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Just confirms app is running
});
```

## Distributed Caching

```csharp
// Redis caching with resilience
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "MyApp:";
});

public class CachedProductService(
    IProductRepository repository,
    IDistributedCache cache,
    ILogger<CachedProductService> logger)
{
    private static readonly ResiliencePipeline CachePipeline =
        new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromMilliseconds(100))
            .AddFallback(new FallbackStrategyOptions
            {
                ShouldHandle = _ => PredicateResult.True(),
                FallbackAction = _ => Outcome.FromResultAsValueTask<object?>(null)
            })
            .Build();

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        var cacheKey = $"product:{id}";
        
        // Try cache with timeout and fallback
        var cached = await CachePipeline.ExecuteAsync(async token =>
        {
            var data = await cache.GetStringAsync(cacheKey, token);
            return data != null ? JsonSerializer.Deserialize<Product>(data) : null;
        }, ct);

        if (cached != null)
            return cached;

        // Get from database
        var product = await repository.GetByIdAsync(id, ct);
        
        if (product != null)
        {
            // Cache asynchronously, don't block
            _ = Task.Run(async () =>
            {
                try
                {
                    await cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(product),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                        });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to cache product {ProductId}", id);
                }
            });
        }

        return product;
    }
}
```
