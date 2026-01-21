# Skill: Generate Controller

## When to Use
User requests to create an API controller, endpoints, or REST API.

## Template

```csharp
namespace {Namespace}.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class {Entity}sController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<{Entity}ListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Get{Entity}sQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }
    
    // Additional endpoints...
}
```

## Example Output

### Full CRUD Controller

```csharp
namespace MyApp.Api.Controllers;

/// <summary>
/// Manages customer resources
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CustomersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Get all customers with pagination and filtering
    /// </summary>
    /// <param name="query">Pagination and filter parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of customers</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a customer by ID
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Customer details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerQuery(id), cancellationToken);
        
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(CreateProblemDetails(result.Error, StatusCodes.Status404NotFound));
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    /// <param name="request">Customer creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created customer ID</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(request.Name, request.Email);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Contains("exists")
                ? Conflict(CreateProblemDetails(result.Error, StatusCodes.Status409Conflict))
                : BadRequest(CreateProblemDetails(result.Error, StatusCodes.Status400BadRequest));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            new { id = result.Value });
    }

    /// <summary>
    /// Update an existing customer
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(id, request.Name);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(CreateProblemDetails(result.Error, StatusCodes.Status404NotFound));
    }

    /// <summary>
    /// Deactivate a customer
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateCustomerCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Contains("not found")
                ? NotFound(CreateProblemDetails(result.Error, StatusCodes.Status404NotFound))
                : BadRequest(CreateProblemDetails(result.Error, StatusCodes.Status400BadRequest));
        }

        return NoContent();
    }

    /// <summary>
    /// Delete a customer
    /// </summary>
    /// <param name="id">Customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCustomerCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : NotFound(CreateProblemDetails(result.Error, StatusCodes.Status404NotFound));
    }

    /// <summary>
    /// Get orders for a customer
    /// </summary>
    [HttpGet("{id:int}/orders")]
    [ProducesResponseType(typeof(IEnumerable<OrderListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrders(
        int id,
        [FromQuery] GetCustomerOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query with { CustomerId = id }, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(CreateProblemDetails(result.Error, StatusCodes.Status404NotFound));
    }

    private static ProblemDetails CreateProblemDetails(string detail, int statusCode)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Detail = detail
        };
    }
}
```

### Minimal API Alternative

```csharp
// Endpoints/CustomerEndpoints.cs
public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers")
            .WithOpenApi();

        group.MapGet("/", GetAll)
            .WithName("GetCustomers")
            .Produces<PagedResult<CustomerListItemResponse>>();

        group.MapGet("/{id:int}", GetById)
            .WithName("GetCustomer")
            .Produces<CustomerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", Create)
            .WithName("CreateCustomer")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", Update)
            .WithName("UpdateCustomer")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", Delete)
            .WithName("DeleteCustomer")
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetAll(
        [AsParameters] GetCustomersQuery query,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCustomerQuery(id), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> Create(
        CreateCustomerRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(request.Name, request.Email);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.CreatedAtRoute("GetCustomer", new { id = result.Value })
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> Update(
        int id,
        UpdateCustomerRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCustomerCommand(id, request.Name);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> Delete(
        int id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCustomerCommand(id), cancellationToken);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.NotFound(result.Error);
    }
}
```

## Guidelines

1. **Use ISender (MediatR)** - Decouple from handlers
2. **ProducesResponseType** - Document all possible responses
3. **CancellationToken** - Always accept and pass through
4. **ProblemDetails** - Use standard error format
5. **CreatedAtAction** - Return location header for POST
6. **NoContent** - Use for successful PUT/DELETE
7. **Thin controllers** - Delegate to handlers/services
