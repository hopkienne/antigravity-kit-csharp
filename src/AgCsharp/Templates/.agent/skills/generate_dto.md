# Skill: Generate DTO

## When to Use
User requests to create DTOs (Data Transfer Objects), request/response models, or ViewModels.

## Template

### Request DTO
```csharp
namespace {Namespace}.Application.Features.{Feature}.Commands;

public record Create{Entity}Request(
    {PropertyType} {PropertyName},
    {PropertyType} {PropertyName});
```

### Response DTO
```csharp
namespace {Namespace}.Application.Features.{Feature}.Queries;

public record {Entity}Response(
    int Id,
    {PropertyType} {PropertyName},
    DateTime CreatedAt);
```

## Example Output

### Request DTOs

```csharp
// Create request
public record CreateCustomerRequest(
    [Required]
    [StringLength(100, MinimumLength = 2)]
    string Name,
    
    [Required]
    [EmailAddress]
    string Email,
    
    CreateAddressRequest? Address);

public record CreateAddressRequest(
    [Required]
    [StringLength(200)]
    string Street,
    
    [Required]
    [StringLength(100)]
    string City,
    
    [Required]
    [StringLength(10)]
    string PostalCode);

// Update request
public record UpdateCustomerRequest(
    [Required]
    [StringLength(100, MinimumLength = 2)]
    string Name);

// Partial update (PATCH)
public record PatchCustomerRequest(
    string? Name,
    string? Email);
```

### Response DTOs

```csharp
// Single item response
public record CustomerResponse(
    int Id,
    string Name,
    string Email,
    string Status,
    AddressResponse? Address,
    DateTime CreatedAt);

public record AddressResponse(
    string Street,
    string City,
    string PostalCode,
    string FullAddress);

// List item response (lighter version)
public record CustomerListItemResponse(
    int Id,
    string Name,
    string Email,
    string Status);

// Paginated response
public record PagedResponse<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

### Command/Query DTOs (MediatR)

```csharp
// Command
public record CreateCustomerCommand(
    string Name,
    string Email,
    CreateAddressRequest? Address) : IRequest<Result<int>>;

// Query
public record GetCustomerQuery(int Id) : IRequest<Result<CustomerResponse>>;

public record GetCustomersQuery(
    int Page = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    CustomerStatus? Status = null) : IRequest<PagedResponse<CustomerListItemResponse>>;
```

## AutoMapper Profile

```csharp
namespace {Namespace}.Application.Common.Mappings;

public class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        // Entity to Response
        CreateMap<Customer, CustomerResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        
        CreateMap<Customer, CustomerListItemResponse>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        
        CreateMap<Address, AddressResponse>()
            .ForMember(dest => dest.FullAddress, opt => opt.MapFrom(src => 
                $"{src.Street}, {src.City} {src.PostalCode}"));
        
        // Request to Command (if needed)
        CreateMap<CreateCustomerRequest, CreateCustomerCommand>();
    }
}
```

## Projection with EF Core (Recommended)

```csharp
// Direct projection - more efficient than AutoMapper for queries
public async Task<CustomerResponse?> GetByIdAsync(int id, CancellationToken ct)
{
    return await _context.Customers
        .Where(c => c.Id == id)
        .Select(c => new CustomerResponse(
            c.Id,
            c.Name,
            c.Email,
            c.Status.ToString(),
            c.Address != null 
                ? new AddressResponse(
                    c.Address.Street,
                    c.Address.City,
                    c.Address.PostalCode,
                    $"{c.Address.Street}, {c.Address.City}")
                : null,
            c.CreatedAt))
        .FirstOrDefaultAsync(ct);
}
```

## Guidelines

1. **Use records** - Immutable by default, value equality, concise syntax
2. **Separate request/response** - Different concerns, different validation
3. **Validation on requests** - Use Data Annotations or FluentValidation
4. **No business logic** - DTOs are pure data containers
5. **Flatten when appropriate** - Reduce nesting for simple responses
6. **Use projection** - Query directly to DTO shape for efficiency
7. **Version DTOs** - Create V2 DTOs when breaking changes needed
