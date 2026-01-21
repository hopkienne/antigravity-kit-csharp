# Vertical Slice Architecture

## Overview
Vertical Slice Architecture organizes code by feature rather than by technical layer. Each feature is self-contained with all its components (handler, validator, models) in one place.

```
Traditional (Horizontal):          Vertical Slice:
├── Controllers/                   ├── Features/
│   ├── CustomersController.cs     │   ├── Customers/
│   └── OrdersController.cs        │   │   ├── GetCustomer.cs
├── Services/                      │   │   ├── CreateCustomer.cs
│   ├── CustomerService.cs         │   │   └── DeleteCustomer.cs
│   └── OrderService.cs            │   └── Orders/
├── Repositories/                  │       ├── CreateOrder.cs
│   ├── CustomerRepository.cs      │       └── GetOrder.cs
│   └── OrderRepository.cs         └── Common/
└── Models/                            ├── Behaviors/
    ├── CustomerDto.cs                 └── Extensions/
    └── OrderDto.cs
```

## Project Structure

```
src/
├── MyApp.Api/
│   ├── Features/
│   │   ├── Customers/
│   │   │   ├── GetCustomer.cs         # Query + Handler + Response
│   │   │   ├── GetCustomers.cs        # List query
│   │   │   ├── CreateCustomer.cs      # Command + Handler + Request
│   │   │   ├── UpdateCustomer.cs
│   │   │   └── DeleteCustomer.cs
│   │   ├── Orders/
│   │   │   ├── CreateOrder.cs
│   │   │   ├── GetOrder.cs
│   │   │   └── CancelOrder.cs
│   │   └── Products/
│   │       └── ...
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── LoggingBehavior.cs
│   │   ├── Exceptions/
│   │   ├── Extensions/
│   │   └── Models/
│   ├── Endpoints/
│   │   └── EndpointExtensions.cs      # Minimal API mapping
│   └── Program.cs
│
├── MyApp.Domain/                       # Shared domain entities
│   └── Entities/
│
└── MyApp.Infrastructure/               # Shared infrastructure
    └── Persistence/
```

## Feature File Pattern

### Query Example (Get Single Item)

```csharp
// Features/Customers/GetCustomer.cs
namespace MyApp.Features.Customers;

public static class GetCustomer
{
    // Request/Query
    public record Query(int Id) : IRequest<Result<Response>>;

    // Response DTO
    public record Response(
        int Id,
        string Name,
        string Email,
        DateTime CreatedAt);

    // Handler
    public class Handler(ApplicationDbContext db) 
        : IRequestHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(
            Query request, 
            CancellationToken cancellationToken)
        {
            var customer = await db.Customers
                .Where(c => c.Id == request.Id)
                .Select(c => new Response(
                    c.Id,
                    c.Name,
                    c.Email,
                    c.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);

            return customer is null
                ? Result<Response>.Failure("Customer not found")
                : Result<Response>.Success(customer);
        }
    }

    // Endpoint mapping
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/{id:int}", async (
            int id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new Query(id), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(result.Error);
        })
        .WithName("GetCustomer")
        .WithTags("Customers")
        .Produces<Response>()
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
```

### Command Example (Create Item)

```csharp
// Features/Customers/CreateCustomer.cs
namespace MyApp.Features.Customers;

public static class CreateCustomer
{
    // Request DTO
    public record Request(string Name, string Email);

    // Command
    public record Command(string Name, string Email) : IRequest<Result<int>>;

    // Validator
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();
        }
    }

    // Handler
    public class Handler(ApplicationDbContext db) 
        : IRequestHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(
            Command request, 
            CancellationToken cancellationToken)
        {
            var customer = new Customer
            {
                Name = request.Name,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow
            };

            db.Customers.Add(customer);
            await db.SaveChangesAsync(cancellationToken);

            return Result<int>.Success(customer.Id);
        }
    }

    // Endpoint mapping
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new Command(request.Name, request.Email);
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.CreatedAtRoute("GetCustomer", 
                    new { id = result.Value }, 
                    new { id = result.Value })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateCustomer")
        .WithTags("Customers")
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status201Created);
    }
}
```

## Endpoint Registration

```csharp
// Endpoints/EndpointExtensions.cs
namespace MyApp.Endpoints;

public static class EndpointExtensions
{
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        // Customers
        GetCustomer.MapEndpoint(app);
        GetCustomers.MapEndpoint(app);
        CreateCustomer.MapEndpoint(app);
        UpdateCustomer.MapEndpoint(app);
        DeleteCustomer.MapEndpoint(app);

        // Orders
        CreateOrder.MapEndpoint(app);
        GetOrder.MapEndpoint(app);
        CancelOrder.MapEndpoint(app);

        return app;
    }
}

// Program.cs
var app = builder.Build();
app.MapFeatureEndpoints();
app.Run();
```

## With MediatR Pipeline Behaviors

```csharp
// Common/Behaviors/ValidationBehavior.cs
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
        
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Key Principles

1. **Feature Cohesion**: All code for a feature lives together
2. **Minimal Dependencies**: Features are independent of each other
3. **Direct Database Access**: Skip repository abstraction when appropriate
4. **CQRS-lite**: Separate read (Query) and write (Command) operations
5. **Discoverability**: Easy to find all code related to a feature

## When to Use

### Use Vertical Slice When:
- Building APIs with distinct features
- Team works on features independently
- CRUD operations dominate
- Rapid development is priority

### Consider Clean Architecture When:
- Complex domain logic
- Shared business rules across features
- Multiple presentation layers (API + Web + Mobile)
- Long-term maintainability is critical
