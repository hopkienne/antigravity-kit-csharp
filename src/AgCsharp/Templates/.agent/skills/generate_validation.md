# Skill: Generate Validation

## When to Use
User requests to create validation rules, validators, or input validation for requests.

## Template

```csharp
namespace {Namespace}.Application.Features.{Feature}.Commands;

public class {Command}Validator : AbstractValidator<{Command}>
{
    public {Command}Validator()
    {
        RuleFor(x => x.{Property})
            .NotEmpty()
            .WithMessage("{Property} is required");
    }
}
```

## Example Output

### Command Validator

```csharp
namespace MyApp.Application.Features.Customers.Commands;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    private readonly ICustomerRepository _customerRepository;

    public CreateCustomerCommandValidator(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MinimumLength(2)
            .WithMessage("Name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .MustAsync(BeUniqueEmail)
            .WithMessage("Email already exists");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Invalid phone number format");

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today.AddYears(-18))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Customer must be at least 18 years old");

        RuleFor(x => x.Address)
            .SetValidator(new AddressValidator()!)
            .When(x => x.Address != null);
    }

    private async Task<bool> BeUniqueEmail(
        string email, 
        CancellationToken cancellationToken)
    {
        return !await _customerRepository.EmailExistsAsync(email, cancellationToken);
    }
}
```

### Nested Object Validator

```csharp
public class AddressValidator : AbstractValidator<CreateAddressRequest>
{
    public AddressValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty()
            .WithMessage("Street is required")
            .MaximumLength(200);

        RuleFor(x => x.City)
            .NotEmpty()
            .WithMessage("City is required")
            .MaximumLength(100);

        RuleFor(x => x.PostalCode)
            .NotEmpty()
            .WithMessage("Postal code is required")
            .Matches(@"^\d{5}(-\d{4})?$")
            .WithMessage("Invalid postal code format");

        RuleFor(x => x.Country)
            .NotEmpty()
            .WithMessage("Country is required")
            .Length(2)
            .WithMessage("Country must be ISO 2-letter code");
    }
}
```

### Collection Validator

```csharp
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("Invalid customer ID");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item")
            .Must(items => items.Count <= 50)
            .WithMessage("Order cannot have more than 50 items");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemValidator());

        RuleFor(x => x.Items)
            .Must(HaveUniqueProducts)
            .WithMessage("Order cannot have duplicate products");

        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .WithMessage("Shipping address is required")
            .SetValidator(new AddressValidator()!);
    }

    private bool HaveUniqueProducts(List<OrderItemRequest> items)
    {
        return items.Select(i => i.ProductId).Distinct().Count() == items.Count;
    }
}

public class OrderItemValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Invalid product ID");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be at least 1")
            .LessThanOrEqualTo(100)
            .WithMessage("Quantity cannot exceed 100");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0)
            .When(x => x.UnitPrice.HasValue)
            .WithMessage("Unit price must be positive");
    }
}
```

### Custom Validation Rules

```csharp
public static class CustomValidators
{
    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]")
            .WithMessage("Password must contain at least one number")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one special character");
    }

    public static IRuleBuilderOptions<T, string> MustBeValidSlug<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens");
    }

    public static IRuleBuilderOptions<T, decimal> MustBeValidCurrency<T>(
        this IRuleBuilder<T, decimal> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("Amount cannot be negative")
            .PrecisionScale(18, 2, false)
            .WithMessage("Amount can have at most 2 decimal places");
    }
}

// Usage
public class UserRegistrationValidator : AbstractValidator<RegisterUserCommand>
{
    public UserRegistrationValidator()
    {
        RuleFor(x => x.Password)
            .MustBeStrongPassword();
        
        RuleFor(x => x.Username)
            .MustBeValidSlug();
    }
}
```

### MediatR Validation Pipeline

```csharp
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

### DI Registration

```csharp
// In DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    // Register all validators from assembly
    services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
    
    // Register validation pipeline
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    
    return services;
}
```

## Guidelines

1. **One validator per command** - Keep validators focused
2. **Use async for DB checks** - MustAsync for repository calls
3. **Validate nested objects** - Use SetValidator for child validators
4. **Custom error messages** - Be specific and helpful
5. **Conditional validation** - Use When/Unless for optional fields
6. **Collection validation** - Use RuleForEach for items
7. **Reusable rules** - Create extension methods for common patterns
