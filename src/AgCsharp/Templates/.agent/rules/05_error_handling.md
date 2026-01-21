# Error Handling

## Global Exception Handling

### Exception Handling Middleware

```csharp
// Middleware/ExceptionHandlingMiddleware.cs
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
                CreateValidationProblemDetails(validationEx)),
            
            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Title = "Resource Not Found",
                    Detail = notFoundEx.Message,
                    Status = StatusCodes.Status404NotFound
                }),
            
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = "You are not authorized to access this resource",
                    Status = StatusCodes.Status401Unauthorized
                }),
            
            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = conflictEx.Message,
                    Status = StatusCodes.Status409Conflict
                }),
            
            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Title = "An error occurred",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Status = StatusCodes.Status500InternalServerError
                })
        };

        // Log the exception
        logger.LogError(exception, 
            "Exception occurred: {Message}", 
            exception.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(
        ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest
        };
    }
}

// Register in Program.cs
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

## Custom Exceptions

```csharp
// Domain/Exceptions/DomainException.cs
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}

public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }
}

// Usage
public async Task<Order> GetOrderAsync(int id)
{
    var order = await _repository.GetByIdAsync(id);
    return order ?? throw new NotFoundException(nameof(Order), id);
}
```

## Result Pattern

### Result Class Implementation

```csharp
// Common/Models/Result.cs
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Success result cannot have error");
        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Failure result must have error");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, string.Empty);
    public static Result<T> Failure<T>(string error) => new(default!, false, error);
}

public class Result<T> : Result
{
    public T Value { get; }

    protected internal Result(T value, bool isSuccess, string error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}

// Extended errors with codes
public record Error(string Code, string Message)
{
    public static Error None => new(string.Empty, string.Empty);
    public static Error NotFound(string entity) => new("NotFound", $"{entity} was not found");
    public static Error Validation(string message) => new("Validation", message);
    public static Error Conflict(string message) => new("Conflict", message);
}
```

### Using Result Pattern

```csharp
// In Application Layer
public class CreateOrderHandler(
    ICustomerRepository customerRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        // Validate customer exists
        var customer = await customerRepository.GetByIdAsync(
            request.CustomerId, cancellationToken);
        
        if (customer is null)
            return Result.Failure<int>("Customer not found");

        // Validate customer can place orders
        if (!customer.CanPlaceOrders)
            return Result.Failure<int>("Customer is not allowed to place orders");

        // Create order
        var order = Order.Create(customer.Id, request.Items);
        
        await orderRepository.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(order.Id);
    }
}

// In Controller
[HttpPost]
public async Task<IActionResult> CreateOrder(
    CreateOrderRequest request,
    CancellationToken cancellationToken)
{
    var command = new CreateOrderCommand(request.CustomerId, request.Items);
    var result = await _sender.Send(command, cancellationToken);

    if (result.IsFailure)
        return BadRequest(new ProblemDetails { Detail = result.Error });

    return CreatedAtAction(
        nameof(GetOrder), 
        new { id = result.Value }, 
        new { id = result.Value });
}
```

## ProblemDetails Response Format

### Standard ProblemDetails

```csharp
// Configure ProblemDetails in Program.cs
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = 
            context.HttpContext.TraceIdentifier;
    };
});

// Example response:
// {
//   "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
//   "title": "Not Found",
//   "status": 404,
//   "detail": "Customer with ID 123 was not found.",
//   "instance": "/api/customers/123",
//   "traceId": "00-abc123-def456-00"
// }
```

## Validation with FluentValidation

```csharp
// MediatR Pipeline Behavior
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Logging Best Practices

```csharp
// Structured logging with context
public async Task<Order> ProcessOrderAsync(int orderId)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["OrderId"] = orderId,
        ["Operation"] = "ProcessOrder"
    });

    _logger.LogInformation("Starting order processing");

    try
    {
        var order = await _repository.GetByIdAsync(orderId);
        
        _logger.LogInformation(
            "Order retrieved. Status: {Status}, Total: {Total}",
            order.Status,
            order.Total);

        // Process...

        _logger.LogInformation("Order processing completed successfully");
        return order;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, 
            "Order processing failed for order {OrderId}", 
            orderId);
        throw;
    }
}
```
