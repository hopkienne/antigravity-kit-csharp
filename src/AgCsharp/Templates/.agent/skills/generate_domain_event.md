# Skill: Generate Domain Event

## When to Use
User requests to create domain events and event handlers for significant state changes.

## Template

### Domain Event
```csharp
namespace {Namespace}.Domain.Events;

public record {EntityName}{Action}Event(
    {PropertyType} {PropertyName}) : DomainEvent;
```

### Event Handler
```csharp
namespace {Namespace}.Application.EventHandlers;

public class {EntityName}{Action}EventHandler(
    I{Dependency} dependency,
    ILogger<{EntityName}{Action}EventHandler> logger)
    : INotificationHandler<{EntityName}{Action}Event>
{
    public async Task Handle(
        {EntityName}{Action}Event notification,
        CancellationToken cancellationToken)
    {
        // Handle the event
    }
}
```

## Example Output

### Domain Event Base Class

```csharp
namespace MyApp.Domain.Common;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

### Order Domain Events

```csharp
namespace MyApp.Domain.Events;

// Order lifecycle events
public record OrderCreatedEvent(
    int OrderId,
    int CustomerId) : DomainEvent;

public record OrderItemAddedEvent(
    int OrderId,
    int ProductId,
    int Quantity) : DomainEvent;

public record OrderItemRemovedEvent(
    int OrderId,
    int ProductId) : DomainEvent;

public record OrderSubmittedEvent(
    int OrderId,
    int CustomerId,
    Money Total) : DomainEvent;

public record OrderConfirmedEvent(
    int OrderId) : DomainEvent;

public record OrderShippedEvent(
    int OrderId,
    string TrackingNumber) : DomainEvent;

public record OrderCancelledEvent(
    int OrderId,
    string Reason) : DomainEvent;

// Customer events
public record CustomerCreatedEvent(
    int CustomerId,
    Email Email) : DomainEvent;

public record CustomerEmailChangedEvent(
    int CustomerId,
    Email NewEmail) : DomainEvent;

public record CustomerDeactivatedEvent(
    int CustomerId) : DomainEvent;
```

### Event Handlers

```csharp
namespace MyApp.Application.EventHandlers;

// Send confirmation email when order is submitted
public class OrderSubmittedEventHandler(
    IEmailService emailService,
    ICustomerRepository customerRepository,
    ILogger<OrderSubmittedEventHandler> logger)
    : INotificationHandler<OrderSubmittedEvent>
{
    public async Task Handle(
        OrderSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling OrderSubmittedEvent for Order {OrderId}",
            notification.OrderId);

        var customer = await customerRepository.GetByIdAsync(
            notification.CustomerId, cancellationToken);

        if (customer is null)
        {
            logger.LogWarning(
                "Customer {CustomerId} not found for order confirmation",
                notification.CustomerId);
            return;
        }

        await emailService.SendOrderConfirmationAsync(
            customer.Email.Value,
            notification.OrderId,
            notification.Total,
            cancellationToken);

        logger.LogInformation(
            "Order confirmation email sent for Order {OrderId}",
            notification.OrderId);
    }
}

// Reserve inventory when order is submitted
public class ReserveInventoryOnOrderSubmittedHandler(
    IInventoryService inventoryService,
    IOrderRepository orderRepository,
    ILogger<ReserveInventoryOnOrderSubmittedHandler> logger)
    : INotificationHandler<OrderSubmittedEvent>
{
    public async Task Handle(
        OrderSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Reserving inventory for Order {OrderId}",
            notification.OrderId);

        var order = await orderRepository.GetWithItemsAsync(
            notification.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogError("Order {OrderId} not found", notification.OrderId);
            return;
        }

        foreach (var item in order.Items)
        {
            await inventoryService.ReserveAsync(
                item.ProductId,
                item.Quantity,
                notification.OrderId,
                cancellationToken);
        }

        logger.LogInformation(
            "Inventory reserved for Order {OrderId} ({ItemCount} items)",
            notification.OrderId,
            order.Items.Count);
    }
}

// Notify warehouse when order ships
public class OrderShippedEventHandler(
    INotificationService notificationService,
    ILogger<OrderShippedEventHandler> logger)
    : INotificationHandler<OrderShippedEvent>
{
    public async Task Handle(
        OrderShippedEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Order {OrderId} shipped with tracking {TrackingNumber}",
            notification.OrderId,
            notification.TrackingNumber);

        await notificationService.SendShippingNotificationAsync(
            notification.OrderId,
            notification.TrackingNumber,
            cancellationToken);
    }
}

// Release inventory when order is cancelled
public class ReleaseInventoryOnOrderCancelledHandler(
    IInventoryService inventoryService,
    ILogger<ReleaseInventoryOnOrderCancelledHandler> logger)
    : INotificationHandler<OrderCancelledEvent>
{
    public async Task Handle(
        OrderCancelledEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Releasing inventory for cancelled Order {OrderId}. Reason: {Reason}",
            notification.OrderId,
            notification.Reason);

        await inventoryService.ReleaseReservationAsync(
            notification.OrderId,
            cancellationToken);
    }
}
```

### Domain Event Dispatcher

```csharp
namespace MyApp.Infrastructure.Persistence;

public class DomainEventDispatcher(IMediator mediator)
{
    public async Task DispatchEventsAsync(
        IEnumerable<AggregateRoot> aggregates,
        CancellationToken cancellationToken = default)
    {
        var domainEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
```

### Integration with DbContext

```csharp
public class ApplicationDbContext : DbContext
{
    private readonly IMediator _mediator;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Get aggregates with domain events
        var aggregatesWithEvents = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // Save changes first
        var result = await base.SaveChangesAsync(cancellationToken);

        // Then dispatch events
        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
```

### Integration Event (For Cross-Service Communication)

```csharp
namespace MyApp.Application.IntegrationEvents;

// Integration event for external services
public record OrderSubmittedIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int OrderId { get; init; }
    public int CustomerId { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "USD";
    public List<OrderItemDto> Items { get; init; } = [];
}

// Handler that publishes to message bus
public class PublishOrderSubmittedIntegrationEventHandler(
    IMessageBus messageBus,
    IOrderRepository orderRepository,
    ILogger<PublishOrderSubmittedIntegrationEventHandler> logger)
    : INotificationHandler<OrderSubmittedEvent>
{
    public async Task Handle(
        OrderSubmittedEvent notification,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetWithItemsAsync(
            notification.OrderId, cancellationToken);

        if (order is null) return;

        var integrationEvent = new OrderSubmittedIntegrationEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Total = order.Total.Amount,
            Currency = order.Total.Currency,
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice.Amount
            }).ToList()
        };

        await messageBus.PublishAsync(
            "orders.submitted",
            integrationEvent,
            cancellationToken);

        logger.LogInformation(
            "Published OrderSubmittedIntegrationEvent for Order {OrderId}",
            notification.OrderId);
    }
}
```

## Guidelines

1. **Use records** - Immutable, concise, value equality
2. **Past tense naming** - OrderCreated, not CreateOrder
3. **Include relevant data** - IDs and essential state
4. **Keep handlers focused** - One responsibility per handler
5. **Log in handlers** - Trace event processing
6. **Dispatch after save** - Ensure consistency
7. **Separate domain/integration** - Domain events internal, integration events external
