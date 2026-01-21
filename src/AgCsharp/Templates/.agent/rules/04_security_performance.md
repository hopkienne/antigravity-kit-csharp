# Security & Performance Rules

## Security

### Input Validation

#### Always Validate User Input
```csharp
// Use FluentValidation for complex validation
public class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Invalid customer ID");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .LessThanOrEqualTo(100);
        });
    }
}

// Use Data Annotations for simple validation
public record CreateCustomerRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = default!;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = default!;
}
```

### SQL Injection Prevention

#### NEVER Use String Concatenation for SQL
```csharp
// ❌ NEVER DO THIS - SQL Injection vulnerable
var sql = $"SELECT * FROM Customers WHERE Name = '{name}'";

// ✅ Use parameterized queries
var customers = await context.Customers
    .Where(c => c.Name == name)
    .ToListAsync();

// ✅ If raw SQL needed, use parameters
var customers = await context.Customers
    .FromSqlInterpolated($"SELECT * FROM Customers WHERE Name = {name}")
    .ToListAsync();

// ✅ With Dapper - always use parameters
var customers = await connection.QueryAsync<Customer>(
    "SELECT * FROM Customers WHERE Name = @Name",
    new { Name = name });
```

### Authentication & Authorization

#### Secure Endpoints
```csharp
// Require authentication
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    // Require specific role
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id) { }

    // Require specific policy
    [Authorize(Policy = "CanManageOrders")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateOrderRequest request) { }
}

// Configure policies in Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageOrders", policy =>
        policy.RequireClaim("permission", "orders:manage"));
});
```

#### Secure Sensitive Data
```csharp
// Use secrets management
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVault:Url"]!),
    new DefaultAzureCredential());

// Never log sensitive data
public async Task<IActionResult> Login(LoginRequest request)
{
    _logger.LogInformation("Login attempt for user {Email}", request.Email);
    // ❌ Never log passwords or tokens
    // _logger.LogInformation("Password: {Password}", request.Password);
}

// Hash passwords - use built-in Identity or BCrypt
var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
var isValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
```

### CORS Configuration
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://myapp.com", "https://admin.myapp.com")
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Content-Type", "Authorization")
              .AllowCredentials();
    });
});

// ❌ Avoid in production
// policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
```

### Rate Limiting
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 100;
        limiterOptions.QueueLimit = 10;
    });

    options.AddTokenBucketLimiter("authenticated", limiterOptions =>
    {
        limiterOptions.TokenLimit = 1000;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        limiterOptions.TokensPerPeriod = 100;
    });
});

app.UseRateLimiter();

[EnableRateLimiting("api")]
public class PublicController : ControllerBase { }
```

---

## Performance

### Async/Await Best Practices

#### Always Use Async for I/O Operations
```csharp
// ✅ Proper async usage
public async Task<Customer?> GetCustomerAsync(int id, CancellationToken ct)
{
    return await _context.Customers
        .FirstOrDefaultAsync(c => c.Id == id, ct);
}

// ❌ Don't block on async
public Customer? GetCustomer(int id)
{
    // This can cause deadlocks!
    return _context.Customers
        .FirstOrDefaultAsync(c => c.Id == id).Result;
}

// ✅ Use ConfigureAwait(false) in library code
public async Task<string> GetDataAsync()
{
    var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
}
```

#### Parallel Operations
```csharp
// ✅ Run independent operations in parallel
public async Task<DashboardData> GetDashboardAsync(CancellationToken ct)
{
    var customersTask = _customerService.GetCountAsync(ct);
    var ordersTask = _orderService.GetRecentAsync(ct);
    var revenueTask = _revenueService.GetTotalAsync(ct);

    await Task.WhenAll(customersTask, ordersTask, revenueTask);

    return new DashboardData(
        CustomerCount: await customersTask,
        RecentOrders: await ordersTask,
        TotalRevenue: await revenueTask);
}
```

### Database Performance

#### Efficient Querying
```csharp
// ✅ Project only needed columns
var customers = await _context.Customers
    .Select(c => new CustomerDto(c.Id, c.Name, c.Email))
    .ToListAsync();

// ❌ Don't load entire entities when not needed
var customers = await _context.Customers.ToListAsync();

// ✅ Use pagination
var customers = await _context.Customers
    .OrderBy(c => c.Name)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// ✅ Use AsNoTracking for read-only queries
var customers = await _context.Customers
    .AsNoTracking()
    .Where(c => c.Status == Status.Active)
    .ToListAsync();
```

#### Avoid N+1 Queries
```csharp
// ❌ N+1 problem - executes query for each order
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await _context.Customers.FindAsync(order.CustomerId);
}

// ✅ Use eager loading
var orders = await _context.Orders
    .Include(o => o.Customer)
    .ToListAsync();

// ✅ Or use projection
var orders = await _context.Orders
    .Select(o => new OrderDto(
        o.Id,
        o.Total,
        o.Customer.Name))
    .ToListAsync();
```

#### Database Indexing
```csharp
// In Entity Configuration
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Index on frequently queried columns
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
        
        // Composite index for common query patterns
        builder.HasIndex(c => new { c.Status, c.CreatedAt });
    }
}
```

### Caching

#### Use Response Caching
```csharp
[HttpGet]
[ResponseCache(Duration = 60, VaryByQueryKeys = ["category"])]
public async Task<IActionResult> GetProducts(string category)
{
    return Ok(await _productService.GetByCategoryAsync(category));
}
```

#### Use Distributed Caching
```csharp
public class CachedProductService(
    IProductRepository repository,
    IDistributedCache cache)
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        var cacheKey = $"product:{id}";
        
        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached != null)
            return JsonSerializer.Deserialize<Product>(cached);

        var product = await repository.GetByIdAsync(id, ct);
        if (product != null)
        {
            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(product),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                },
                ct);
        }

        return product;
    }
}
```

### Memory Management

```csharp
// ✅ Use object pooling for frequent allocations
private static readonly ObjectPool<StringBuilder> StringBuilderPool =
    ObjectPool.Create<StringBuilder>();

public string BuildReport(IEnumerable<ReportItem> items)
{
    var sb = StringBuilderPool.Get();
    try
    {
        foreach (var item in items)
            sb.AppendLine(item.ToString());
        return sb.ToString();
    }
    finally
    {
        sb.Clear();
        StringBuilderPool.Return(sb);
    }
}

// ✅ Use Span<T> for high-performance scenarios
public static int CountDigits(ReadOnlySpan<char> input)
{
    int count = 0;
    foreach (var c in input)
        if (char.IsDigit(c)) count++;
    return count;
}
```
