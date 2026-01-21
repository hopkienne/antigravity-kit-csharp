# Clean Architecture

## Overview
Clean Architecture organizes code into concentric layers with dependencies pointing inward. The core business logic has no dependencies on external frameworks or infrastructure.

```
┌─────────────────────────────────────────────────┐
│                 Presentation                     │
│            (Controllers, Views, API)             │
├─────────────────────────────────────────────────┤
│                Infrastructure                    │
│      (Database, External Services, Files)        │
├─────────────────────────────────────────────────┤
│                  Application                     │
│         (Use Cases, DTOs, Interfaces)            │
├─────────────────────────────────────────────────┤
│                    Domain                        │
│         (Entities, Value Objects, Events)        │
└─────────────────────────────────────────────────┘
```

## Project Structure

```
src/
├── MyApp.Domain/                    # Core business logic
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Events/
│   ├── Exceptions/
│   └── Interfaces/                  # Repository interfaces
│
├── MyApp.Application/               # Use cases & orchestration
│   ├── Common/
│   │   ├── Behaviors/               # MediatR pipeline behaviors
│   │   ├── Interfaces/              # Application service interfaces
│   │   └── Models/                  # DTOs, Results
│   ├── Features/
│   │   ├── Customers/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   └── EventHandlers/
│   │   └── Orders/
│   └── DependencyInjection.cs
│
├── MyApp.Infrastructure/            # External concerns
│   ├── Persistence/
│   │   ├── DbContext/
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   └── Migrations/
│   ├── Services/
│   │   ├── EmailService.cs
│   │   └── FileStorageService.cs
│   └── DependencyInjection.cs
│
└── MyApp.Api/                       # Presentation layer
    ├── Controllers/
    ├── Middleware/
    ├── Filters/
    └── Program.cs
```

## Layer Rules

### Domain Layer (Innermost)
- **No dependencies** on other layers or external packages
- Contains: Entities, Value Objects, Domain Events, Domain Services
- Pure business logic, no I/O operations
- Repository interfaces defined here (not implementations)

```csharp
// Domain/Entities/Customer.cs
namespace MyApp.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public CustomerStatus Status { get; private set; }

    private Customer() { } // EF Core

    public static Customer Create(string name, Email email)
    {
        var customer = new Customer
        {
            Name = name,
            Email = email,
            Status = CustomerStatus.Active
        };
        
        customer.AddDomainEvent(new CustomerCreatedEvent(customer.Id));
        return customer;
    }

    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            throw new DomainException("Customer is already inactive");
            
        Status = CustomerStatus.Inactive;
        AddDomainEvent(new CustomerDeactivatedEvent(Id));
    }
}
```

### Application Layer
- Depends only on Domain layer
- Contains: Use Cases (Commands/Queries), DTOs, Validators
- Orchestrates domain objects to perform tasks
- Defines interfaces for infrastructure services

```csharp
// Application/Features/Customers/Commands/CreateCustomer.cs
namespace MyApp.Application.Features.Customers.Commands;

public record CreateCustomerCommand(string Name, string Email) 
    : IRequest<Result<int>>;

public class CreateCustomerCommandHandler(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCustomerCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        CreateCustomerCommand request, 
        CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        if (email.IsFailure)
            return Result<int>.Failure(email.Error);

        var customer = Customer.Create(request.Name, email.Value);
        
        await repository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result<int>.Success(customer.Id);
    }
}
```

### Infrastructure Layer
- Depends on Domain and Application layers
- Implements interfaces defined in inner layers
- Contains: Database access, External API clients, File storage

```csharp
// Infrastructure/Persistence/Repositories/CustomerRepository.cs
namespace MyApp.Infrastructure.Persistence.Repositories;

public class CustomerRepository(ApplicationDbContext context) 
    : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(
        int id, 
        CancellationToken cancellationToken = default)
    {
        return await context.Customers
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        Customer customer, 
        CancellationToken cancellationToken = default)
    {
        await context.Customers.AddAsync(customer, cancellationToken);
    }
}
```

### Presentation Layer (Outermost)
- Depends on Application layer (and Infrastructure for DI registration)
- Contains: Controllers, Views, Middleware, Filters
- Handles HTTP concerns, authentication, response formatting

```csharp
// Api/Controllers/CustomersController.cs
namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(request.Name, request.Email);
        var result = await sender.Send(command, cancellationToken);
        
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, null)
            : BadRequest(result.Error);
    }
}
```

## Dependency Injection Setup

```csharp
// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg => 
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        
        services.AddTransient(typeof(IPipelineBehavior<,>), 
            typeof(ValidationBehavior<,>));
        
        return services;
    }
}

// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        return services;
    }
}

// Program.cs
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

## Key Principles
1. **Dependency Rule**: Dependencies point inward only
2. **Abstraction**: Inner layers define interfaces, outer layers implement
3. **Testability**: Business logic is isolated and testable without infrastructure
4. **Independence**: Core business logic doesn't depend on frameworks
5. **Flexibility**: Easy to swap infrastructure components
