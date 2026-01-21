# Skill: Generate Entity

## When to Use
User requests to create a domain entity, model, or database table representation.

## Template

```csharp
namespace {Namespace}.Domain.Entities;

/// <summary>
/// Represents a {EntityName} in the domain.
/// </summary>
public class {EntityName} : BaseEntity
{
    // Properties
    public {PropertyType} {PropertyName} { get; private set; }
    
    // Navigation properties (if applicable)
    public virtual ICollection<{RelatedEntity}> {RelatedEntities} { get; private set; } = [];
    
    // Private constructor for EF Core
    private {EntityName}() { }
    
    // Factory method for creating new instances
    public static {EntityName} Create({parameters})
    {
        var entity = new {EntityName}
        {
            // Set properties
        };
        
        // Add domain event
        entity.AddDomainEvent(new {EntityName}CreatedEvent(entity.Id));
        
        return entity;
    }
    
    // Domain methods that encapsulate business logic
    public void {ActionMethod}({parameters})
    {
        // Validate invariants
        if (/* validation fails */)
            throw new DomainException("Validation message");
        
        // Apply changes
        
        // Raise domain event if significant
        AddDomainEvent(new {EntityName}{Action}Event(Id));
    }
}
```

## Example Output

```csharp
namespace MyApp.Domain.Entities;

/// <summary>
/// Represents a Customer in the domain.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; private set; } = default!;
    public Email Email { get; private set; } = default!;
    public CustomerStatus Status { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    
    public virtual ICollection<Order> Orders { get; private set; } = [];
    
    private Customer() { }
    
    public static Customer Create(string name, Email email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        var customer = new Customer
        {
            Name = name.Trim(),
            Email = email,
            Status = CustomerStatus.Active
        };
        
        customer.AddDomainEvent(new CustomerCreatedEvent(customer.Id, email));
        
        return customer;
    }
    
    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
    
    public void UpdateEmail(Email email)
    {
        Email = email;
        AddDomainEvent(new CustomerEmailChangedEvent(Id, email));
    }
    
    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            throw new DomainException("Customer is already inactive");
        
        Status = CustomerStatus.Inactive;
        DeactivatedAt = DateTime.UtcNow;
        
        AddDomainEvent(new CustomerDeactivatedEvent(Id));
    }
    
    public void Reactivate()
    {
        if (Status == CustomerStatus.Active)
            throw new DomainException("Customer is already active");
        
        Status = CustomerStatus.Active;
        DeactivatedAt = null;
        
        AddDomainEvent(new CustomerReactivatedEvent(Id));
    }
    
    public bool CanPlaceOrder()
    {
        return Status == CustomerStatus.Active;
    }
}

public enum CustomerStatus
{
    Active,
    Inactive,
    Suspended
}
```

## Base Entity Class

```csharp
public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    private readonly List<IDomainEvent> _domainEvents = [];
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

## Guidelines

1. **Use private setters** - Control mutations through methods
2. **Factory methods** - Use static Create() for object creation
3. **Validate invariants** - Throw DomainException for invalid states
4. **Domain events** - Raise events for significant state changes
5. **Encapsulation** - Keep business logic within the entity
6. **Private constructor** - Required for EF Core, prevents invalid objects
