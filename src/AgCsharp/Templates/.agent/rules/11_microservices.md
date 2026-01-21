# Microservices Design

## Service Boundaries

### Principles for Defining Boundaries

1. **Business Capability**: Each service owns a complete business function
2. **Bounded Context**: Align with DDD bounded contexts
3. **Team Ownership**: One team owns one or more services
4. **Data Ownership**: Each service owns its data

```
┌─────────────────────────────────────────────────────────────────┐
│                    E-Commerce Microservices                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐ │
│  │  Customer  │  │   Order    │  │  Catalog   │  │  Payment   │ │
│  │  Service   │  │  Service   │  │  Service   │  │  Service   │ │
│  └──────┬─────┘  └──────┬─────┘  └──────┬─────┘  └──────┬─────┘ │
│         │               │               │               │        │
│  ┌──────┴─────┐  ┌──────┴─────┐  ┌──────┴─────┐  ┌──────┴─────┐ │
│  │ Customer   │  │  Order     │  │  Product   │  │  Payment   │ │
│  │    DB      │  │    DB      │  │    DB      │  │    DB      │ │
│  └────────────┘  └────────────┘  └────────────┘  └────────────┘ │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Service Decomposition Patterns

```csharp
// ❌ Bad - Monolith disguised as microservices
public class OrderService
{
    public async Task<Order> CreateOrder(CreateOrderRequest request)
    {
        // Directly calling other databases
        var customer = await _customerDb.Customers.FindAsync(request.CustomerId);
        var product = await _catalogDb.Products.FindAsync(request.ProductId);
        var payment = await _paymentDb.ProcessPayment(request.Payment);
        
        // Cross-cutting transaction
        using var transaction = await _orderDb.BeginTransactionAsync();
        // ...
    }
}

// ✅ Good - Proper service boundaries
public class OrderService
{
    public async Task<Order> CreateOrder(CreateOrderRequest request)
    {
        // Call other services via HTTP/gRPC
        var customer = await _customerServiceClient.GetAsync(request.CustomerId);
        var product = await _catalogServiceClient.GetAsync(request.ProductId);
        
        // Own data only
        var order = Order.Create(request.CustomerId, product.Id, product.Price);
        await _orderRepository.AddAsync(order);
        
        // Async communication for payment
        await _messageQueue.PublishAsync(new OrderCreatedEvent(order.Id));
        
        return order;
    }
}
```

## API Contracts

### OpenAPI Specification

```yaml
# order-service-api.yaml
openapi: 3.0.3
info:
  title: Order Service API
  version: 1.0.0
  
paths:
  /api/orders:
    post:
      summary: Create a new order
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateOrderRequest'
      responses:
        '201':
          description: Order created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/OrderResponse'
        '400':
          $ref: '#/components/responses/BadRequest'
          
components:
  schemas:
    CreateOrderRequest:
      type: object
      required:
        - customerId
        - items
      properties:
        customerId:
          type: integer
        items:
          type: array
          items:
            $ref: '#/components/schemas/OrderItemRequest'
```

### Contract-First Development

```csharp
// Generate client from OpenAPI spec
// dotnet add package NSwag.ApiDescription.Client

// In .csproj
<OpenApiReference Include="order-service-api.yaml">
    <Namespace>MyApp.OrderServiceClient</Namespace>
</OpenApiReference>

// Usage
public class CheckoutService(OrderServiceClient orderClient)
{
    public async Task<OrderResponse> PlaceOrderAsync(CheckoutRequest request)
    {
        var createOrderRequest = new CreateOrderRequest
        {
            CustomerId = request.CustomerId,
            Items = request.Items.Select(i => new OrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        return await orderClient.CreateOrderAsync(createOrderRequest);
    }
}
```

## Service Communication

### Synchronous (HTTP/gRPC)

```csharp
// HTTP Client with resilience
builder.Services.AddHttpClient<ICustomerServiceClient, CustomerServiceClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Services:Customer:Url"]!);
})
.AddStandardResilienceHandler(); // .NET 8+ Resilience

// gRPC Client
builder.Services.AddGrpcClient<CustomerService.CustomerServiceClient>(options =>
{
    options.Address = new Uri(configuration["Services:Customer:GrpcUrl"]!);
});
```

### Asynchronous (Message Queue)

```csharp
// Publishing events
public class OrderCreatedEventPublisher(IMessageBus messageBus)
{
    public async Task PublishAsync(Order order)
    {
        var @event = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Total = order.Total,
            CreatedAt = DateTime.UtcNow
        };

        await messageBus.PublishAsync("order.created", @event);
    }
}

// Consuming events
public class OrderCreatedEventHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent message, CancellationToken ct)
    {
        // Process the event
        await _inventoryService.ReserveStockAsync(message.OrderId, ct);
    }
}
```

## Service Discovery

### Using Configuration
```json
{
  "Services": {
    "Customer": {
      "Url": "https://customer-service:8080",
      "GrpcUrl": "https://customer-service:8081"
    },
    "Order": {
      "Url": "https://order-service:8080"
    }
  }
}
```

### Using Service Mesh (Kubernetes)
```yaml
# Kubernetes Service
apiVersion: v1
kind: Service
metadata:
  name: customer-service
spec:
  selector:
    app: customer-service
  ports:
    - port: 80
      targetPort: 8080
---
# In application, use service name
# http://customer-service/api/customers
```

## API Gateway

```csharp
// Using YARP (Yet Another Reverse Proxy)
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "customers-route": {
        "ClusterId": "customers-cluster",
        "Match": {
          "Path": "/api/customers/{**catch-all}"
        }
      },
      "orders-route": {
        "ClusterId": "orders-cluster",
        "Match": {
          "Path": "/api/orders/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "customers-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://customer-service:8080"
          }
        }
      },
      "orders-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "https://order-service:8080"
          }
        }
      }
    }
  }
}
```

## Data Consistency

### Saga Pattern for Distributed Transactions

```csharp
public class CreateOrderSaga
{
    private readonly List<ICompensatableStep> _steps = [];

    public async Task<Result> ExecuteAsync(CreateOrderRequest request)
    {
        try
        {
            // Step 1: Reserve inventory
            var reserveStep = new ReserveInventoryStep(_inventoryService, request.Items);
            await reserveStep.ExecuteAsync();
            _steps.Add(reserveStep);

            // Step 2: Process payment
            var paymentStep = new ProcessPaymentStep(_paymentService, request.Payment);
            await paymentStep.ExecuteAsync();
            _steps.Add(paymentStep);

            // Step 3: Create order
            var orderStep = new CreateOrderStep(_orderService, request);
            await orderStep.ExecuteAsync();
            _steps.Add(orderStep);

            return Result.Success();
        }
        catch (Exception ex)
        {
            // Compensate in reverse order
            foreach (var step in _steps.AsEnumerable().Reverse())
            {
                await step.CompensateAsync();
            }
            
            return Result.Failure(ex.Message);
        }
    }
}

public interface ICompensatableStep
{
    Task ExecuteAsync();
    Task CompensateAsync();
}
```

## Best Practices

1. **Database per service**: Each service owns its database
2. **API versioning**: Always version your APIs
3. **Health checks**: Implement health endpoints
4. **Centralized logging**: Use correlation IDs across services
5. **Circuit breakers**: Protect against cascade failures
6. **Idempotency**: Design for safe retries
7. **Event-driven**: Prefer async communication
