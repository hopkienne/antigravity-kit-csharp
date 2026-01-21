# DDD Tactical Patterns

## Aggregates

### Aggregate Root Definition
An Aggregate is a cluster of domain objects treated as a single unit. The Aggregate Root is the entry point and guardian of the Aggregate's invariants.

```csharp
public abstract class AggregateRoot : Entity
{
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

### Order Aggregate Example
```csharp
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];
    
    public int CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // EF Core

    public static Order Create(int customerId)
    {
        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            Total = Money.Zero
        };
        
        order.AddDomainEvent(new OrderCreatedEvent(order.Id, customerId));
        return order;
    }

    public void AddItem(Product product, int quantity)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Cannot modify a submitted order");

        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == product.Id);
        
        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(product.Id, product.Price, quantity));
        }

        RecalculateTotal();
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Order is not in draft status");

        if (!_items.Any())
            throw new DomainException("Cannot submit empty order");

        Status = OrderStatus.Submitted;
        AddDomainEvent(new OrderSubmittedEvent(Id, Total));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
            throw new DomainException("Cannot cancel shipped/delivered order");

        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    private void RecalculateTotal()
    {
        Total = _items.Aggregate(
            Money.Zero,
            (sum, item) => sum + item.Subtotal);
    }
}
```

### Aggregate Rules
1. **Reference only by ID**: External aggregates reference only by ID, not by object reference
2. **One transaction per aggregate**: Save one aggregate per transaction
3. **Invariants within boundary**: All invariants must be satisfied within the aggregate
4. **Small aggregates**: Keep aggregates small; split if too large

## Value Objects

### Characteristics
- **Immutable**: Cannot be changed after creation
- **Equality by value**: Two Value Objects are equal if all properties are equal
- **Self-validating**: Validate in constructor
- **No identity**: No ID property

### Value Object Base Class
```csharp
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
```

### Value Object Examples
```csharp
// Email Value Object
public class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<Email>("Email cannot be empty");

        if (!IsValidEmail(email))
            return Result.Failure<Email>("Invalid email format");

        return Result.Success(new Email(email.ToLowerInvariant()));
    }

    private static bool IsValidEmail(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
    
    public static implicit operator string(Email email) => email.Value;
}

// Money Value Object
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");
        
        return new Money(amount, currency);
    }

    public static Money Zero => new(0, "USD");

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity)
    {
        return new Money(money.Amount * quantity, money.Currency);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}

// Address Value Object
public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string state, 
                   string postalCode, string country)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        State = state ?? throw new ArgumentNullException(nameof(state));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
        Country = country ?? throw new ArgumentNullException(nameof(country));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
}
```

## Domain Events

### Domain Event Definition
```csharp
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
    Guid EventId { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
```

### Domain Event Examples
```csharp
public record OrderCreatedEvent(int OrderId, int CustomerId) : DomainEvent;

public record OrderSubmittedEvent(int OrderId, Money Total) : DomainEvent;

public record OrderCancelledEvent(int OrderId, string Reason) : DomainEvent;

public record CustomerRegisteredEvent(int CustomerId, Email Email) : DomainEvent;
```

### Domain Event Handlers
```csharp
public class OrderSubmittedEventHandler(
    IEmailService emailService,
    ILogger<OrderSubmittedEventHandler> logger)
    : INotificationHandler<OrderSubmittedEvent>
{
    public async Task Handle(
        OrderSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Order {OrderId} submitted with total {Total}",
            notification.OrderId,
            notification.Total);

        await emailService.SendOrderConfirmationAsync(
            notification.OrderId,
            cancellationToken);
    }
}
```

### Publishing Domain Events
```csharp
public class DomainEventDispatcher(IMediator mediator)
{
    public async Task DispatchEventsAsync(
        IEnumerable<AggregateRoot> aggregates,
        CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                await mediator.Publish(domainEvent, cancellationToken);
            }
            
            aggregate.ClearDomainEvents();
        }
    }
}

// In SaveChangesAsync
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var aggregates = ChangeTracker.Entries<AggregateRoot>()
        .Select(e => e.Entity)
        .Where(e => e.DomainEvents.Any())
        .ToList();

    var result = await base.SaveChangesAsync(ct);

    await _domainEventDispatcher.DispatchEventsAsync(aggregates, ct);

    return result;
}
```

## Repository Pattern

```csharp
// Generic repository interface in Domain layer
public interface IRepository<T> where T : AggregateRoot
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

// Specific repository with domain-specific queries
public interface IOrderRepository : IRepository<Order>
{
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        int customerId, 
        CancellationToken ct = default);
    
    Task<Order?> GetWithItemsAsync(
        int orderId, 
        CancellationToken ct = default);
}
```
