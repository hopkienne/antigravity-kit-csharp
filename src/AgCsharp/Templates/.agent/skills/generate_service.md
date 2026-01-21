# Skill: Generate Service

## When to Use
User requests to create a service class for business logic, application services, or use case handlers.

## Template (Application Service)

```csharp
namespace {Namespace}.Application.Services;

public interface I{Entity}Service
{
    Task<Result<{Entity}Response>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(Create{Entity}Request request, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, Update{Entity}Request request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public class {Entity}Service(
    I{Entity}Repository repository,
    IUnitOfWork unitOfWork,
    ILogger<{Entity}Service> logger) : I{Entity}Service
{
    // Implementation
}
```

## Example Output

### Interface

```csharp
namespace MyApp.Application.Services;

public interface ICustomerService
{
    Task<Result<CustomerResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<PagedResult<CustomerListItemResponse>>> GetAllAsync(
        GetCustomersQuery query, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken ct = default);
    Task<Result> DeactivateAsync(int id, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
```

### Implementation

```csharp
namespace MyApp.Application.Services;

public class CustomerService(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork,
    ILogger<CustomerService> logger) : ICustomerService
{
    public async Task<Result<CustomerResponse>> GetByIdAsync(
        int id, 
        CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        
        if (customer is null)
        {
            logger.LogWarning("Customer {CustomerId} not found", id);
            return Result.Failure<CustomerResponse>($"Customer {id} not found");
        }

        return Result.Success(MapToResponse(customer));
    }

    public async Task<Result<PagedResult<CustomerListItemResponse>>> GetAllAsync(
        GetCustomersQuery query, 
        CancellationToken ct = default)
    {
        var (customers, totalCount) = await customerRepository.GetPagedAsync(
            query.Page,
            query.PageSize,
            query.SearchTerm,
            query.Status,
            ct);

        var items = customers.Select(c => new CustomerListItemResponse(
            c.Id,
            c.Name,
            c.Email.Value,
            c.Status.ToString()));

        return Result.Success(new PagedResult<CustomerListItemResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount));
    }

    public async Task<Result<int>> CreateAsync(
        CreateCustomerRequest request, 
        CancellationToken ct = default)
    {
        // Check for duplicate email
        if (await customerRepository.EmailExistsAsync(request.Email, ct))
        {
            logger.LogWarning("Attempted to create customer with existing email {Email}", 
                request.Email);
            return Result.Failure<int>("Email already exists");
        }

        // Create email value object
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<int>(emailResult.Error);

        // Create customer entity
        var customer = Customer.Create(request.Name, emailResult.Value);
        
        // Add address if provided
        if (request.Address is not null)
        {
            customer.SetAddress(new Address(
                request.Address.Street,
                request.Address.City,
                request.Address.PostalCode,
                "US"));
        }

        await customerRepository.AddAsync(customer, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Customer {CustomerId} created with email {Email}", 
            customer.Id, customer.Email.Value);

        return Result.Success(customer.Id);
    }

    public async Task<Result> UpdateAsync(
        int id, 
        UpdateCustomerRequest request, 
        CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        
        if (customer is null)
            return Result.Failure($"Customer {id} not found");

        customer.UpdateName(request.Name);
        
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Customer {CustomerId} updated", id);

        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        
        if (customer is null)
            return Result.Failure($"Customer {id} not found");

        try
        {
            customer.Deactivate();
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Customer {CustomerId} deactivated", id);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Failed to deactivate customer {CustomerId}", id);
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        
        if (customer is null)
            return Result.Failure($"Customer {id} not found");

        customerRepository.Remove(customer);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Customer {CustomerId} deleted", id);

        return Result.Success();
    }

    private static CustomerResponse MapToResponse(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email.Value,
            customer.Status.ToString(),
            customer.Address is not null
                ? new AddressResponse(
                    customer.Address.Street,
                    customer.Address.City,
                    customer.Address.PostalCode,
                    customer.Address.ToString())
                : null,
            customer.CreatedAt);
    }
}
```

## MediatR Handler Alternative

```csharp
namespace MyApp.Application.Features.Customers.Commands;

public record CreateCustomerCommand(string Name, string Email) : IRequest<Result<int>>;

public class CreateCustomerCommandHandler(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateCustomerCommandHandler> logger)
    : IRequestHandler<CreateCustomerCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        CreateCustomerCommand request, 
        CancellationToken cancellationToken)
    {
        if (await customerRepository.EmailExistsAsync(request.Email, cancellationToken))
            return Result.Failure<int>("Email already exists");

        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<int>(emailResult.Error);

        var customer = Customer.Create(request.Name, emailResult.Value);
        
        await customerRepository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Customer {CustomerId} created", customer.Id);

        return Result.Success(customer.Id);
    }
}
```

## DI Registration

```csharp
// In Application DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    // Services
    services.AddScoped<ICustomerService, CustomerService>();
    services.AddScoped<IOrderService, OrderService>();
    
    // Or with MediatR
    services.AddMediatR(cfg => 
        cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
    
    return services;
}
```

## Guidelines

1. **Use Result pattern** - Return Result<T> instead of throwing exceptions
2. **Validate inputs** - Check for invalid or duplicate data
3. **Log important actions** - Create, update, delete operations
4. **Keep thin** - Orchestrate, don't contain business logic (that's domain's job)
5. **Handle domain exceptions** - Catch and convert to Result failures
6. **Use cancellation tokens** - Support request cancellation
