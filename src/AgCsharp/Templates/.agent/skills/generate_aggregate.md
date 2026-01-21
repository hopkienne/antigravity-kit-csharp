# Skill: Generate Aggregate Root

## When to Use
User requests to create a DDD Aggregate Root with invariants, child entities, and domain events.

## Template

```csharp
namespace {Namespace}.Domain.Aggregates.{AggregateName};

public class {AggregateName} : AggregateRoot
{
    private readonly List<{ChildEntity}> _{childEntities} = [];
    
    // Properties with private setters
    public {ValueObject} {Property} { get; private set; }
    public {AggregateState} Status { get; private set; }
    
    // Read-only collection
    public IReadOnlyList<{ChildEntity}> {ChildEntities} => _{childEntities}.AsReadOnly();
    
    // Private constructor for EF Core
    private {AggregateName}() { }
    
    // Factory method
    public static {AggregateName} Create({parameters})
    {
        // Validate invariants
        // Create aggregate
        // Raise domain event
    }
    
    // Domain methods
    public void {Action}({parameters})
    {
        // Validate invariants
        // Apply state change
        // Raise domain event
    }
}
```

## Example Output

### Order Aggregate

```csharp
namespace MyApp.Domain.Aggregates.Orders;

/// <summary>
/// Order Aggregate Root - manages the lifecycle of an order and its items.
/// Invariants:
/// - Order must have at least one item when submitted
/// - Cannot modify items after order is submitted
/// - Total must equal sum of item subtotals
/// </summary>
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];

    public int CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money Subtotal { get; private set; }
    public Money Tax { get; private set; }
    public Money Total { get; private set; }
    public Address? ShippingAddress { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // EF Core

    #region Factory Methods

    public static Order Create(int customerId)
    {
        if (customerId <= 0)
            throw new ArgumentException("Invalid customer ID", nameof(customerId));

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            Subtotal = Money.Zero,
            Tax = Money.Zero,
            Total = Money.Zero
        };

        order.AddDomainEvent(new OrderCreatedEvent(order.Id, customerId));
        
        return order;
    }

    #endregion

    #region Item Management

    public void AddItem(int productId, string productName, Money unitPrice, int quantity)
    {
        EnsureOrderIsEditable();

        if (quantity <= 0)
            throw new DomainException("Quantity must be positive");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = OrderItem.Create(productId, productName, unitPrice, quantity);
            _items.Add(item);
        }

        RecalculateTotals();
        
        AddDomainEvent(new OrderItemAddedEvent(Id, productId, quantity));
    }

    public void UpdateItemQuantity(int productId, int newQuantity)
    {
        EnsureOrderIsEditable();

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainException($"Product {productId} not found in order");

        if (newQuantity <= 0)
        {
            RemoveItem(productId);
            return;
        }

        item.UpdateQuantity(newQuantity);
        RecalculateTotals();
    }

    public void RemoveItem(int productId)
    {
        EnsureOrderIsEditable();

        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainException($"Product {productId} not found in order");

        _items.Remove(item);
        RecalculateTotals();
        
        AddDomainEvent(new OrderItemRemovedEvent(Id, productId));
    }

    #endregion

    #region Order Lifecycle

    public void SetShippingAddress(Address address)
    {
        EnsureOrderIsEditable();
        ShippingAddress = address ?? throw new ArgumentNullException(nameof(address));
    }

    public void Submit()
    {
        EnsureOrderIsEditable();

        if (!_items.Any())
            throw new DomainException("Cannot submit an order without items");

        if (ShippingAddress is null)
            throw new DomainException("Shipping address is required");

        Status = OrderStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderSubmittedEvent(Id, CustomerId, Total));
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Submitted)
            throw new DomainException("Only submitted orders can be confirmed");

        Status = OrderStatus.Confirmed;
        
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }

    public void Ship(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException("Only confirmed orders can be shipped");

        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new ArgumentException("Tracking number is required", nameof(trackingNumber));

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderShippedEvent(Id, trackingNumber));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
            throw new DomainException("Cannot cancel shipped or delivered orders");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required", nameof(reason));

        Status = OrderStatus.Cancelled;

        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    #endregion

    #region Private Methods

    private void EnsureOrderIsEditable()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Order can only be modified in Draft status");
    }

    private void RecalculateTotals()
    {
        Subtotal = _items.Aggregate(Money.Zero, (sum, item) => sum + item.Subtotal);
        Tax = Subtotal * TaxRate;
        Total = Subtotal + Tax;
    }

    private const decimal TaxRate = 0.10m;

    #endregion
}
```

### Child Entity

```csharp
namespace MyApp.Domain.Aggregates.Orders;

/// <summary>
/// OrderItem is a child entity within the Order aggregate.
/// It has identity within the aggregate but cannot exist independently.
/// </summary>
public class OrderItem : Entity
{
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public Money UnitPrice { get; private set; } = default!;
    public int Quantity { get; private set; }
    public Money Subtotal { get; private set; } = default!;

    private OrderItem() { } // EF Core

    internal static OrderItem Create(
        int productId, 
        string productName, 
        Money unitPrice, 
        int quantity)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Subtotal = unitPrice * quantity
        };
    }

    internal void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new DomainException("Quantity must be positive");

        Quantity = newQuantity;
        RecalculateSubtotal();
    }

    internal void IncreaseQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new DomainException("Additional quantity must be positive");

        Quantity += additionalQuantity;
        RecalculateSubtotal();
    }

    private void RecalculateSubtotal()
    {
        Subtotal = UnitPrice * Quantity;
    }
}
```

### Order Status Enum

```csharp
public enum OrderStatus
{
    Draft,
    Submitted,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}
```

### EF Core Configuration

```csharp
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        
        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.Status)
            .HasConversion<string>();
        
        // Value Object - Owned Type
        builder.OwnsOne(o => o.Subtotal, money => ConfigureMoney(money, "Subtotal"));
        builder.OwnsOne(o => o.Tax, money => ConfigureMoney(money, "Tax"));
        builder.OwnsOne(o => o.Total, money => ConfigureMoney(money, "Total"));
        builder.OwnsOne(o => o.ShippingAddress);
        
        // Child entities
        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("OrderItems");
            item.WithOwner().HasForeignKey("OrderId");
            item.HasKey("Id");
            item.OwnsOne(i => i.UnitPrice, money => ConfigureMoney(money, "UnitPrice"));
            item.OwnsOne(i => i.Subtotal, money => ConfigureMoney(money, "Subtotal"));
        });
        
        // Ignore domain events
        builder.Ignore(o => o.DomainEvents);
    }
    
    private void ConfigureMoney<T>(OwnedNavigationBuilder<T, Money> builder, string prefix) 
        where T : class
    {
        builder.Property(m => m.Amount).HasColumnName($"{prefix}Amount");
        builder.Property(m => m.Currency).HasColumnName($"{prefix}Currency").HasMaxLength(3);
    }
}
```

## Guidelines

1. **Single aggregate per transaction** - Don't modify multiple aggregates
2. **Reference by ID** - Reference other aggregates by ID only
3. **Small aggregates** - Keep aggregates focused and small
4. **Internal access for children** - Child entities modified only through root
5. **Factory methods** - Use static Create() for controlled construction
6. **Invariant protection** - Validate all state changes
7. **Domain events** - Raise events for significant state changes
