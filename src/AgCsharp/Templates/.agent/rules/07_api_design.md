# REST API Design

## HTTP Methods

| Method | Purpose | Idempotent | Request Body | Response |
|--------|---------|------------|--------------|----------|
| GET | Retrieve resource(s) | Yes | No | Resource data |
| POST | Create resource | No | Yes | Created resource |
| PUT | Replace resource | Yes | Yes | Updated resource |
| PATCH | Partial update | Yes | Yes | Updated resource |
| DELETE | Remove resource | Yes | No | No content |

## URL Structure

### Resource Naming
```
# ✅ Good - plural nouns, lowercase, hyphens for multi-word
GET    /api/customers
GET    /api/customers/{id}
GET    /api/customers/{id}/orders
GET    /api/order-items

# ❌ Bad
GET    /api/getCustomers          # No verbs
GET    /api/Customer/{id}         # No singular/PascalCase
GET    /api/customer_orders       # No underscores
```

### Nested Resources
```
# Customer's orders
GET    /api/customers/{customerId}/orders
POST   /api/customers/{customerId}/orders

# Limit nesting to 2 levels - use filtering for deeper
GET    /api/orders?customerId=123&status=pending
```

## Status Codes

### Success Codes
```csharp
// 200 OK - Successful GET, PUT, PATCH
return Ok(customer);

// 201 Created - Successful POST
return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);

// 204 No Content - Successful DELETE or PUT with no response body
return NoContent();
```

### Client Error Codes
```csharp
// 400 Bad Request - Invalid input, validation errors
return BadRequest(new ProblemDetails { Detail = "Invalid email format" });

// 401 Unauthorized - Authentication required
return Unauthorized();

// 403 Forbidden - Authenticated but not authorized
return Forbid();

// 404 Not Found - Resource doesn't exist
return NotFound(new ProblemDetails { Detail = $"Customer {id} not found" });

// 409 Conflict - Conflicting state (e.g., duplicate email)
return Conflict(new ProblemDetails { Detail = "Email already exists" });

// 422 Unprocessable Entity - Valid syntax but semantic errors
return UnprocessableEntity(validationErrors);
```

### Server Error Codes
```csharp
// 500 Internal Server Error - Unexpected errors (handled by middleware)
// 503 Service Unavailable - Temporary unavailability
```

## API Versioning

### URL Path Versioning (Recommended)
```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Controller
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class CustomersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) { }
}

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class CustomersV2Controller : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) { }
}
```

### Header Versioning (Alternative)
```csharp
options.ApiVersionReader = new HeaderApiVersionReader("X-Api-Version");
```

## Response Envelope

### Standard Response Format
```csharp
// For single items
{
    "data": {
        "id": 1,
        "name": "John Doe",
        "email": "john@example.com"
    }
}

// For collections with pagination
{
    "data": [...],
    "pagination": {
        "currentPage": 1,
        "pageSize": 10,
        "totalPages": 5,
        "totalCount": 47
    }
}

// For errors (using ProblemDetails)
{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Email format is invalid",
    "instance": "/api/customers",
    "traceId": "abc123"
}
```

### Response Wrapper Implementation
```csharp
public class ApiResponse<T>
{
    public T? Data { get; init; }
    public PaginationMeta? Pagination { get; init; }
    
    public static ApiResponse<T> Success(T data) => new() { Data = data };
    
    public static ApiResponse<IEnumerable<T>> Paginated(
        IEnumerable<T> data, 
        int page, 
        int pageSize, 
        int totalCount) => new()
    {
        Data = data,
        Pagination = new PaginationMeta(page, pageSize, totalCount)
    };
}

public record PaginationMeta(int CurrentPage, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}
```

## Pagination

### Query Parameters
```
GET /api/customers?page=1&pageSize=20&sortBy=name&sortOrder=asc
```

### Implementation
```csharp
public record PaginatedQuery(
    int Page = 1,
    int PageSize = 10,
    string? SortBy = null,
    string SortOrder = "asc");

[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] PaginatedQuery query,
    CancellationToken cancellationToken)
{
    var (customers, totalCount) = await _service.GetPagedAsync(
        query.Page,
        query.PageSize,
        query.SortBy,
        query.SortOrder,
        cancellationToken);

    Response.Headers.Add("X-Total-Count", totalCount.ToString());
    Response.Headers.Add("X-Page", query.Page.ToString());
    Response.Headers.Add("X-Page-Size", query.PageSize.ToString());

    return Ok(ApiResponse<CustomerDto>.Paginated(
        customers, 
        query.Page, 
        query.PageSize, 
        totalCount));
}
```

## Filtering & Searching

```csharp
public record CustomerFilter(
    string? Name = null,
    string? Email = null,
    CustomerStatus? Status = null,
    DateTime? CreatedAfter = null);

[HttpGet]
public async Task<IActionResult> GetAll(
    [FromQuery] CustomerFilter filter,
    [FromQuery] PaginatedQuery pagination)
{
    var query = _context.Customers.AsQueryable();

    if (!string.IsNullOrEmpty(filter.Name))
        query = query.Where(c => c.Name.Contains(filter.Name));

    if (!string.IsNullOrEmpty(filter.Email))
        query = query.Where(c => c.Email.Contains(filter.Email));

    if (filter.Status.HasValue)
        query = query.Where(c => c.Status == filter.Status);

    if (filter.CreatedAfter.HasValue)
        query = query.Where(c => c.CreatedAt >= filter.CreatedAfter);

    // Apply pagination and return
}
```

## HATEOAS (Optional)

```csharp
public class CustomerResource
{
    public int Id { get; init; }
    public string Name { get; init; }
    public List<Link> Links { get; init; } = [];
}

public record Link(string Href, string Rel, string Method);

// Usage
var customer = new CustomerResource
{
    Id = 1,
    Name = "John",
    Links = 
    [
        new($"/api/customers/1", "self", "GET"),
        new($"/api/customers/1", "update", "PUT"),
        new($"/api/customers/1", "delete", "DELETE"),
        new($"/api/customers/1/orders", "orders", "GET")
    ]
};
```

## Controller Best Practices

```csharp
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Get customer by ID
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id, 
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerQuery(id), cancellationToken);
        
        return result.IsSuccess 
            ? Ok(result.Value) 
            : NotFound(new ProblemDetails { Detail = result.Error });
    }
}
```
