# Skill: Generate Message Handler

## When to Use
User requests to create message queue handlers for async communication (RabbitMQ, Azure Service Bus, etc.).

## Template

```csharp
public class {MessageName}Handler : IMessageHandler<{MessageName}>
{
    public async Task HandleAsync(
        {MessageName} message,
        CancellationToken cancellationToken)
    {
        // Handle the message
    }
}
```

## Example Output

### Message Definitions

```csharp
namespace MyApp.Messaging.Messages;

// Base message
public abstract record IntegrationMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}

// Order messages
public record OrderCreatedMessage(
    int OrderId,
    int CustomerId,
    decimal Total,
    List<OrderItemMessage> Items) : IntegrationMessage;

public record OrderItemMessage(
    int ProductId,
    int Quantity,
    decimal UnitPrice);

public record OrderCancelledMessage(
    int OrderId,
    string Reason) : IntegrationMessage;

public record OrderShippedMessage(
    int OrderId,
    string TrackingNumber,
    DateTime ShippedAt) : IntegrationMessage;

// Payment messages
public record PaymentProcessedMessage(
    int OrderId,
    decimal Amount,
    string TransactionId,
    PaymentStatus Status) : IntegrationMessage;

public enum PaymentStatus
{
    Succeeded,
    Failed,
    Pending
}
```

### Message Handler Interface

```csharp
namespace MyApp.Messaging;

public interface IMessageHandler<in TMessage> where TMessage : IntegrationMessage
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}
```

### Order Created Handler (Inventory Service)

```csharp
namespace MyApp.Inventory.Handlers;

public class OrderCreatedHandler(
    IInventoryRepository inventoryRepository,
    IUnitOfWork unitOfWork,
    IMessagePublisher messagePublisher,
    ILogger<OrderCreatedHandler> logger)
    : IMessageHandler<OrderCreatedMessage>
{
    public async Task HandleAsync(
        OrderCreatedMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = message.MessageId,
            ["OrderId"] = message.OrderId,
            ["CorrelationId"] = message.CorrelationId
        });

        logger.LogInformation("Processing OrderCreatedMessage for Order {OrderId}", 
            message.OrderId);

        try
        {
            foreach (var item in message.Items)
            {
                var inventory = await inventoryRepository.GetByProductIdAsync(
                    item.ProductId, cancellationToken);

                if (inventory is null)
                {
                    logger.LogWarning(
                        "Product {ProductId} not found in inventory",
                        item.ProductId);
                    continue;
                }

                if (inventory.AvailableQuantity < item.Quantity)
                {
                    logger.LogWarning(
                        "Insufficient stock for Product {ProductId}. Available: {Available}, Requested: {Requested}",
                        item.ProductId,
                        inventory.AvailableQuantity,
                        item.Quantity);

                    // Publish stock shortage event
                    await messagePublisher.PublishAsync(
                        new StockShortageMessage(
                            item.ProductId,
                            message.OrderId,
                            item.Quantity,
                            inventory.AvailableQuantity),
                        cancellationToken);

                    continue;
                }

                inventory.Reserve(item.Quantity, message.OrderId);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish reservation completed
            await messagePublisher.PublishAsync(
                new InventoryReservedMessage(message.OrderId),
                cancellationToken);

            logger.LogInformation(
                "Inventory reserved for Order {OrderId}",
                message.OrderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to process OrderCreatedMessage for Order {OrderId}",
                message.OrderId);
            throw; // Let the messaging infrastructure handle retry
        }
    }
}
```

### Payment Processed Handler (Order Service)

```csharp
namespace MyApp.Orders.Handlers;

public class PaymentProcessedHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ILogger<PaymentProcessedHandler> logger)
    : IMessageHandler<PaymentProcessedMessage>
{
    public async Task HandleAsync(
        PaymentProcessedMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing PaymentProcessedMessage for Order {OrderId}. Status: {Status}",
            message.OrderId,
            message.Status);

        var order = await orderRepository.GetByIdAsync(
            message.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogError("Order {OrderId} not found", message.OrderId);
            return; // Don't retry - order doesn't exist
        }

        switch (message.Status)
        {
            case PaymentStatus.Succeeded:
                order.ConfirmPayment(message.TransactionId);
                logger.LogInformation(
                    "Order {OrderId} payment confirmed. Transaction: {TransactionId}",
                    message.OrderId,
                    message.TransactionId);
                break;

            case PaymentStatus.Failed:
                order.Cancel("Payment failed");
                logger.LogWarning(
                    "Order {OrderId} cancelled due to payment failure",
                    message.OrderId);
                break;

            case PaymentStatus.Pending:
                logger.LogInformation(
                    "Order {OrderId} payment is pending",
                    message.OrderId);
                return; // Don't save, wait for final status
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

### RabbitMQ Consumer

```csharp
namespace MyApp.Messaging.RabbitMQ;

public class RabbitMQConsumer<TMessage> : BackgroundService
    where TMessage : IntegrationMessage
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMQConsumer<TMessage>> _logger;
    private readonly string _queueName;

    public RabbitMQConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMQConsumer<TMessage>> logger,
        string queueName)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queueName = queueName;
        _channel = _connection.CreateModel();
        
        // Configure channel
        _channel.QueueDeclare(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);
        
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(messageJson);
                if (message is null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    _channel.BasicReject(ea.DeliveryTag, requeue: false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<IMessageHandler<TMessage>>();

                await handler.HandleAsync(message, stoppingToken);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
                
                _logger.LogDebug(
                    "Message {MessageId} processed successfully",
                    message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                
                // Retry logic - could check retry count in headers
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: _queueName,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
    }
}
```

### Message Publisher

```csharp
namespace MyApp.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : IntegrationMessage;
}

public class RabbitMQPublisher(
    IConnection connection,
    ILogger<RabbitMQPublisher> logger) : IMessagePublisher
{
    public Task PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : IntegrationMessage
    {
        using var channel = connection.CreateModel();
        
        var exchangeName = GetExchangeName<TMessage>();
        var routingKey = GetRoutingKey<TMessage>();
        
        channel.ExchangeDeclare(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.MessageId.ToString();
        properties.Timestamp = new AmqpTimestamp(
            new DateTimeOffset(message.Timestamp).ToUnixTimeSeconds());
        properties.CorrelationId = message.CorrelationId;
        properties.ContentType = "application/json";

        channel.BasicPublish(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        logger.LogDebug(
            "Published {MessageType} with ID {MessageId}",
            typeof(TMessage).Name,
            message.MessageId);

        return Task.CompletedTask;
    }

    private static string GetExchangeName<TMessage>() =>
        typeof(TMessage).Name.Replace("Message", "").ToLowerInvariant();

    private static string GetRoutingKey<TMessage>() =>
        typeof(TMessage).Name.ToLowerInvariant();
}
```

### DI Registration

```csharp
// In DependencyInjection.cs
public static IServiceCollection AddMessaging(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // RabbitMQ connection
    services.AddSingleton<IConnection>(sp =>
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"],
            UserName = configuration["RabbitMQ:Username"],
            Password = configuration["RabbitMQ:Password"],
            DispatchConsumersAsync = true
        };
        return factory.CreateConnection();
    });

    // Publisher
    services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

    // Handlers
    services.AddScoped<IMessageHandler<OrderCreatedMessage>, OrderCreatedHandler>();
    services.AddScoped<IMessageHandler<PaymentProcessedMessage>, PaymentProcessedHandler>();

    // Background consumers
    services.AddHostedService(sp => 
        new RabbitMQConsumer<OrderCreatedMessage>(
            sp.GetRequiredService<IConnection>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<RabbitMQConsumer<OrderCreatedMessage>>>(),
            "order-created-queue"));

    return services;
}
```

## Guidelines

1. **Idempotency** - Handle duplicate messages gracefully
2. **Correlation IDs** - Track messages across services
3. **Dead letter queues** - Handle poison messages
4. **Retry policies** - Configure appropriate retry behavior
5. **Logging** - Log message processing for debugging
6. **Scoped handlers** - Create new scope per message
7. **Error handling** - Decide: retry, dead-letter, or discard
