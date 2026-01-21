# Skill: Generate gRPC Service

## When to Use
User requests to create a gRPC service for high-performance inter-service communication.

## Template

### Proto File
```protobuf
syntax = "proto3";

option csharp_namespace = "{Namespace}.Grpc";

service {ServiceName} {
  rpc Get{Entity}(Get{Entity}Request) returns ({Entity}Response);
}

message Get{Entity}Request {
  int32 id = 1;
}

message {Entity}Response {
  int32 id = 1;
  string name = 2;
}
```

### Service Implementation
```csharp
public class {ServiceName}GrpcService : {ServiceName}.{ServiceName}Base
{
    public override async Task<{Entity}Response> Get{Entity}(
        Get{Entity}Request request,
        ServerCallContext context)
    {
        // Implementation
    }
}
```

## Example Output

### Proto Definition

```protobuf
// Protos/customer.proto
syntax = "proto3";

option csharp_namespace = "MyApp.Grpc";

import "google/protobuf/timestamp.proto";
import "google/protobuf/wrappers.proto";

package customer;

// Customer Service Definition
service CustomerService {
  // Unary calls
  rpc GetCustomer(GetCustomerRequest) returns (CustomerResponse);
  rpc CreateCustomer(CreateCustomerRequest) returns (CustomerResponse);
  rpc UpdateCustomer(UpdateCustomerRequest) returns (CustomerResponse);
  rpc DeleteCustomer(DeleteCustomerRequest) returns (DeleteCustomerResponse);
  
  // Server streaming
  rpc GetAllCustomers(GetAllCustomersRequest) returns (stream CustomerResponse);
  
  // Client streaming
  rpc ImportCustomers(stream CreateCustomerRequest) returns (ImportCustomersResponse);
  
  // Bidirectional streaming
  rpc CustomerChat(stream CustomerMessage) returns (stream CustomerMessage);
}

// Request/Response Messages
message GetCustomerRequest {
  int32 id = 1;
}

message CreateCustomerRequest {
  string name = 1;
  string email = 2;
  Address address = 3;
}

message UpdateCustomerRequest {
  int32 id = 1;
  string name = 2;
  google.protobuf.StringValue email = 3;  // Nullable
  Address address = 4;
}

message DeleteCustomerRequest {
  int32 id = 1;
}

message DeleteCustomerResponse {
  bool success = 1;
}

message GetAllCustomersRequest {
  int32 page_size = 1;
  string page_token = 2;
  CustomerStatus status_filter = 3;
}

message ImportCustomersResponse {
  int32 imported_count = 1;
  int32 failed_count = 2;
  repeated string errors = 3;
}

message CustomerMessage {
  int32 customer_id = 1;
  string message = 2;
  google.protobuf.Timestamp timestamp = 3;
}

// Domain Messages
message CustomerResponse {
  int32 id = 1;
  string name = 2;
  string email = 3;
  CustomerStatus status = 4;
  Address address = 5;
  google.protobuf.Timestamp created_at = 6;
}

message Address {
  string street = 1;
  string city = 2;
  string state = 3;
  string postal_code = 4;
  string country = 5;
}

enum CustomerStatus {
  CUSTOMER_STATUS_UNSPECIFIED = 0;
  CUSTOMER_STATUS_ACTIVE = 1;
  CUSTOMER_STATUS_INACTIVE = 2;
  CUSTOMER_STATUS_SUSPENDED = 3;
}
```

### Service Implementation

```csharp
namespace MyApp.Grpc.Services;

public class CustomerGrpcService(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork,
    ILogger<CustomerGrpcService> logger)
    : CustomerService.CustomerServiceBase
{
    public override async Task<CustomerResponse> GetCustomer(
        GetCustomerRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC GetCustomer called for ID {Id}", request.Id);
        
        var customer = await customerRepository.GetByIdAsync(
            request.Id,
            context.CancellationToken);

        if (customer is null)
        {
            throw new RpcException(
                new Status(StatusCode.NotFound, $"Customer {request.Id} not found"));
        }

        return MapToResponse(customer);
    }

    public override async Task<CustomerResponse> CreateCustomer(
        CreateCustomerRequest request,
        ServerCallContext context)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, "Name is required"));
        }

        // Check duplicate email
        if (await customerRepository.EmailExistsAsync(request.Email, context.CancellationToken))
        {
            throw new RpcException(
                new Status(StatusCode.AlreadyExists, "Email already exists"));
        }

        // Create customer
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            throw new RpcException(
                new Status(StatusCode.InvalidArgument, emailResult.Error));
        }

        var customer = Customer.Create(request.Name, emailResult.Value);
        
        if (request.Address is not null)
        {
            customer.SetAddress(MapFromProto(request.Address));
        }

        await customerRepository.AddAsync(customer, context.CancellationToken);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Created customer {Id}", customer.Id);

        return MapToResponse(customer);
    }

    public override async Task GetAllCustomers(
        GetAllCustomersRequest request,
        IServerStreamWriter<CustomerResponse> responseStream,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC GetAllCustomers streaming started");

        var customers = await customerRepository.GetAllAsync(context.CancellationToken);

        foreach (var customer in customers)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            await responseStream.WriteAsync(MapToResponse(customer));
            
            // Optional: Add small delay to prevent overwhelming client
            await Task.Delay(10, context.CancellationToken);
        }

        logger.LogInformation("gRPC GetAllCustomers streaming completed");
    }

    public override async Task<ImportCustomersResponse> ImportCustomers(
        IAsyncStreamReader<CreateCustomerRequest> requestStream,
        ServerCallContext context)
    {
        var importedCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            try
            {
                var emailResult = Email.Create(request.Email);
                if (emailResult.IsFailure)
                {
                    failedCount++;
                    errors.Add($"Invalid email for {request.Name}: {emailResult.Error}");
                    continue;
                }

                var customer = Customer.Create(request.Name, emailResult.Value);
                await customerRepository.AddAsync(customer, context.CancellationToken);
                importedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"Failed to import {request.Name}: {ex.Message}");
            }
        }

        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        return new ImportCustomersResponse
        {
            ImportedCount = importedCount,
            FailedCount = failedCount,
            Errors = { errors }
        };
    }

    private static CustomerResponse MapToResponse(Customer customer)
    {
        var response = new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Email = customer.Email.Value,
            Status = (CustomerStatus)(int)customer.Status,
            CreatedAt = Timestamp.FromDateTime(
                DateTime.SpecifyKind(customer.CreatedAt, DateTimeKind.Utc))
        };

        if (customer.Address is not null)
        {
            response.Address = new Address
            {
                Street = customer.Address.Street,
                City = customer.Address.City,
                State = customer.Address.State,
                PostalCode = customer.Address.PostalCode,
                Country = customer.Address.Country
            };
        }

        return response;
    }

    private static Domain.ValueObjects.Address MapFromProto(Address address)
    {
        return Domain.ValueObjects.Address.Create(
            address.Street,
            address.City,
            address.State,
            address.PostalCode,
            address.Country).Value;
    }
}
```

### Server Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add gRPC services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16MB
    options.MaxSendMessageSize = 16 * 1024 * 1024;
});

builder.Services.AddGrpcReflection(); // For testing with grpcurl

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<CustomerGrpcService>();
app.MapGrpcReflectionService(); // Enable reflection

app.Run();
```

### Client Configuration

```csharp
// In consuming service
builder.Services.AddGrpcClient<CustomerService.CustomerServiceClient>(options =>
{
    options.Address = new Uri(configuration["Services:CustomerService:GrpcUrl"]!);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // For development with self-signed certs
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
})
.AddInterceptor<LoggingInterceptor>();

// Usage
public class OrderService(CustomerService.CustomerServiceClient customerClient)
{
    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var customer = await customerClient.GetCustomerAsync(
                new GetCustomerRequest { Id = request.CustomerId },
                cancellationToken: ct);

            // Use customer data...
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return OrderResult.Failure("Customer not found");
        }
    }
}
```

### Interceptor for Logging/Auth

```csharp
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        _logger.LogDebug(
            "gRPC call: {Method}",
            context.Method.FullName);

        return continuation(request, context);
    }
}
```

## Guidelines

1. **Proto-first** - Define proto files, then generate code
2. **Versioning** - Use package names for versioning
3. **Error handling** - Use RpcException with appropriate StatusCode
4. **Streaming** - Use for large datasets or real-time updates
5. **Interceptors** - Add cross-cutting concerns like logging/auth
6. **Reflection** - Enable for development/testing
7. **Message size** - Configure appropriate limits
