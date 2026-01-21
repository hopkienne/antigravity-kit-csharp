# DDD Strategic Patterns

## Bounded Contexts

### Definition
A Bounded Context is a logical boundary within which a particular domain model is defined and applicable. Different contexts may have different meanings for the same term.

### Example: E-Commerce System

```
┌─────────────────────────────────────────────────────────────────┐
│                        E-Commerce System                         │
├──────────────────┬──────────────────┬──────────────────────────┤
│   Sales Context  │ Inventory Context│    Shipping Context      │
├──────────────────┼──────────────────┼──────────────────────────┤
│ • Customer       │ • Product        │ • Shipment               │
│ • Order          │ • Stock          │ • Delivery               │
│ • OrderItem      │ • Warehouse      │ • Address                │
│ • ShoppingCart   │ • StockMovement  │ • Carrier                │
└──────────────────┴──────────────────┴──────────────────────────┘

"Product" means different things:
- Sales: Price, description, images for display
- Inventory: SKU, quantity, location in warehouse
- Shipping: Weight, dimensions for shipping calculation
```

### Context Organization in Code

```
src/
├── Sales/
│   ├── Sales.Domain/
│   │   ├── Aggregates/
│   │   │   ├── Order/
│   │   │   │   ├── Order.cs
│   │   │   │   ├── OrderItem.cs
│   │   │   │   └── OrderStatus.cs
│   │   │   └── Customer/
│   │   ├── Events/
│   │   └── Repositories/
│   ├── Sales.Application/
│   └── Sales.Infrastructure/
│
├── Inventory/
│   ├── Inventory.Domain/
│   ├── Inventory.Application/
│   └── Inventory.Infrastructure/
│
└── Shipping/
    ├── Shipping.Domain/
    ├── Shipping.Application/
    └── Shipping.Infrastructure/
```

## Context Mapping

### Relationship Patterns

```
┌─────────────────────────────────────────────────────────────────┐
│                     Context Mapping Patterns                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────┐  Partnership  ┌─────────┐                          │
│  │ Sales   │◄────────────►│Inventory│  (Equal collaboration)    │
│  └─────────┘               └─────────┘                          │
│                                                                  │
│  ┌─────────┐  Customer/    ┌─────────┐                          │
│  │ Sales   │  Supplier    │ Payment │  (Upstream/Downstream)    │
│  │(Customer)│◄────────────│(Supplier)│                          │
│  └─────────┘               └─────────┘                          │
│                                                                  │
│  ┌─────────┐               ┌─────────┐                          │
│  │ Legacy  │  ACL         │   New   │  (Anti-Corruption Layer)  │
│  │ System  │─────────────►│ System  │                          │
│  └─────────┘               └─────────┘                          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Anti-Corruption Layer (ACL)
Protects your domain from external/legacy systems:

```csharp
// External system's model (what we receive)
public class LegacyOrderDto
{
    public string OrderNumber { get; set; }
    public string CustId { get; set; }
    public List<LegacyOrderLineDto> Lines { get; set; }
}

// Anti-Corruption Layer - Translator
public class LegacyOrderTranslator
{
    public Order Translate(LegacyOrderDto legacyOrder)
    {
        var customerId = ParseCustomerId(legacyOrder.CustId);
        var order = Order.Create(customerId);

        foreach (var line in legacyOrder.Lines)
        {
            var product = MapProduct(line.ItemCode);
            order.AddItem(product, line.Qty);
        }

        return order;
    }

    private int ParseCustomerId(string custId)
    {
        // Handle legacy format: "CUST-00123" -> 123
        return int.Parse(custId.Replace("CUST-", ""));
    }

    private Product MapProduct(string legacyItemCode)
    {
        // Map legacy product codes to domain products
        return _productRepository.GetByLegacyCode(legacyItemCode);
    }
}

// ACL Service
public class LegacyOrderAdapter(
    LegacyOrderTranslator translator,
    IOrderRepository orderRepository)
{
    public async Task ImportOrderAsync(LegacyOrderDto legacyOrder)
    {
        var order = translator.Translate(legacyOrder);
        await orderRepository.AddAsync(order);
    }
}
```

## Ubiquitous Language

### Definition
A shared language between developers and domain experts, used consistently in code, documentation, and conversations.

### Building Ubiquitous Language

```markdown
## Sales Context Glossary

| Term | Definition | Example |
|------|------------|---------|
| Order | A customer's request to purchase products | Order #12345 |
| Order Item | A single product line within an order | 3x iPhone 15 |
| Cart | Temporary collection before order creation | Shopping cart |
| Checkout | Process of converting cart to order | Proceed to checkout |
| Customer | Person or entity making purchases | John Doe |

## Inventory Context Glossary

| Term | Definition | Example |
|------|------------|---------|
| Stock | Available quantity of a product | 150 units in stock |
| SKU | Stock Keeping Unit - unique product identifier | SKU-12345-BLK |
| Warehouse | Physical location storing products | NYC Warehouse |
| Stock Movement | Change in stock quantity | Received 50 units |
| Reservation | Stock held for pending orders | Reserved for Order #123 |
```

### Code Reflects Language

```csharp
// ✅ Good - Uses ubiquitous language
public class Order
{
    public void Submit() { }
    public void Cancel(string reason) { }
    public void AddItem(Product product, int quantity) { }
}

// ❌ Bad - Technical terms instead of domain language
public class OrderEntity
{
    public void SetStatusToSubmitted() { }
    public void MarkAsDeleted() { }
    public void InsertLineItem(ProductEntity product, int qty) { }
}
```

## Domain Services

Services that contain domain logic that doesn't naturally fit within an entity or value object.

```csharp
// Domain Service - Pricing logic involving multiple aggregates
public class PricingService
{
    public Money CalculateOrderTotal(
        Order order,
        Customer customer,
        IEnumerable<Promotion> activePromotions)
    {
        var subtotal = order.Items.Sum(i => i.Subtotal);
        
        // Apply customer-level discount
        var customerDiscount = customer.LoyaltyTier switch
        {
            LoyaltyTier.Gold => 0.10m,
            LoyaltyTier.Silver => 0.05m,
            _ => 0m
        };

        // Apply best promotion
        var promotionDiscount = activePromotions
            .Where(p => p.AppliesTo(order))
            .Max(p => p.DiscountPercentage);

        var totalDiscount = Math.Max(customerDiscount, promotionDiscount);
        
        return subtotal * (1 - totalDiscount);
    }
}

// Domain Service - Transfer between aggregates
public class InventoryTransferService
{
    public void Transfer(
        Warehouse source,
        Warehouse destination,
        Product product,
        int quantity)
    {
        if (!source.HasStock(product, quantity))
            throw new DomainException("Insufficient stock");

        source.RemoveStock(product, quantity);
        destination.AddStock(product, quantity);
    }
}
```

## Integration Between Contexts

### Using Domain Events

```csharp
// Sales Context publishes event
public record OrderPlacedIntegrationEvent(
    int OrderId,
    int CustomerId,
    List<OrderItemDto> Items,
    DateTime PlacedAt);

// Inventory Context handles event
public class OrderPlacedHandler(
    IStockReservationService reservationService)
    : INotificationHandler<OrderPlacedIntegrationEvent>
{
    public async Task Handle(
        OrderPlacedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            await reservationService.ReserveAsync(
                item.ProductId,
                item.Quantity,
                notification.OrderId,
                cancellationToken);
        }
    }
}
```

### Shared Kernel
Shared code between contexts (use sparingly):

```
src/
├── SharedKernel/
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   └── Email.cs
│   ├── Interfaces/
│   │   └── IDomainEvent.cs
│   └── Exceptions/
│       └── DomainException.cs
```

## Context Boundaries Best Practices

1. **One team per context**: Each bounded context owned by one team
2. **Separate databases**: Each context should have its own data store
3. **Explicit contracts**: Clear API contracts between contexts
4. **Async communication**: Prefer events over synchronous calls
5. **Context autonomy**: Each context can be deployed independently
