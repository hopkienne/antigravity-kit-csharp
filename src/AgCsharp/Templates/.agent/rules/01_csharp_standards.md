# C# Coding Standards

## Naming Conventions

### Classes and Interfaces
- Use **PascalCase** for class names: `CustomerService`, `OrderRepository`
- Prefix interfaces with `I`: `ICustomerService`, `IOrderRepository`
- Use nouns or noun phrases for class names
- Avoid abbreviations except well-known ones (Id, Dto, Http)

### Methods
- Use **PascalCase**: `GetCustomerById`, `ProcessOrder`
- Use verbs or verb phrases
- Async methods must have `Async` suffix: `GetCustomerByIdAsync`

### Variables and Parameters
- Use **camelCase**: `customerName`, `orderId`
- Use meaningful, descriptive names
- Avoid single-letter names except in loops (`i`, `j`)
- Prefix private fields with underscore: `_customerRepository`

### Constants
- Use **PascalCase**: `MaxRetryCount`, `DefaultTimeout`
- For compile-time constants, use `const`
- For runtime constants, use `static readonly`

## Null Safety

### Enable Nullable Reference Types
```csharp
// In .csproj
<Nullable>enable</Nullable>
```

### Null Handling Patterns
```csharp
// Use null-conditional operator
var name = customer?.Name;

// Use null-coalescing operator
var displayName = customer?.Name ?? "Unknown";

// Use null-coalescing assignment
_cache ??= new Dictionary<string, object>();

// Use pattern matching for null checks
if (customer is not null)
{
    // Process customer
}

// Avoid null in return types where possible - use Result pattern
public Result<Customer> GetCustomer(int id);
```

## File-Scoped Namespaces
Always use file-scoped namespaces (C# 10+):

```csharp
// ✅ Preferred
namespace MyApp.Services;

public class CustomerService
{
}

// ❌ Avoid
namespace MyApp.Services
{
    public class CustomerService
    {
    }
}
```

## Using Declarations
Prefer using declarations over using statements:

```csharp
// ✅ Preferred
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// ❌ Avoid
using (var connection = new SqlConnection(connectionString))
{
    await connection.OpenAsync();
}
```

## Var Usage
- Use `var` when the type is obvious from the right side
- Use explicit types for clarity when needed

```csharp
// ✅ Good - type is obvious
var customers = new List<Customer>();
var customer = await _repository.GetByIdAsync(id);

// ✅ Good - explicit for clarity
int count = GetCount(); // Not obvious what GetCount returns
```

## Expression-Bodied Members
Use for simple single-expression members:

```csharp
// Properties
public string FullName => $"{FirstName} {LastName}";

// Methods (only if simple)
public override string ToString() => $"Customer: {Name}";

// Constructors (for simple initialization)
public Customer(string name) => Name = name;
```

## Primary Constructors (C# 12+)
Use for simple dependency injection:

```csharp
public class CustomerService(
    ICustomerRepository repository,
    ILogger<CustomerService> logger)
{
    public async Task<Customer?> GetByIdAsync(int id)
    {
        logger.LogInformation("Getting customer {Id}", id);
        return await repository.GetByIdAsync(id);
    }
}
```

## Record Types
Use records for immutable data objects:

```csharp
// Simple record
public record CustomerDto(int Id, string Name, string Email);

// Record with additional members
public record OrderSummary(int OrderId, decimal Total)
{
    public string FormattedTotal => Total.ToString("C");
}
```

## Collection Expressions (C# 12+)
```csharp
// ✅ Preferred
List<int> numbers = [1, 2, 3, 4, 5];
int[] array = [1, 2, 3];
ReadOnlySpan<char> span = ['a', 'b', 'c'];

// Spread operator
int[] combined = [..array1, ..array2];
```

## Pattern Matching
Use pattern matching for type checks and conditionals:

```csharp
// Type patterns
if (obj is Customer customer)
{
    Console.WriteLine(customer.Name);
}

// Switch expressions
var discount = customer.Type switch
{
    CustomerType.Premium => 0.20m,
    CustomerType.Standard => 0.10m,
    CustomerType.New => 0.05m,
    _ => 0m
};

// Property patterns
if (order is { Status: OrderStatus.Pending, Total: > 100 })
{
    // Apply special handling
}
```

## Code Organization
1. Using directives (sorted, grouped)
2. Namespace declaration
3. Type declaration in order:
   - Constants
   - Static fields
   - Instance fields
   - Constructors
   - Properties
   - Methods (public first, then private)
   - Nested types
