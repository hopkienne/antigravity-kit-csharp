# Workflow: Testing

## Overview
Systematic process for writing and running tests.

---

## Test Types

| Type | Purpose | Speed | Isolation |
|------|---------|-------|-----------|
| Unit | Test single unit of code | Fast | High |
| Integration | Test components together | Medium | Medium |
| End-to-End | Test full user flow | Slow | Low |

---

## Step 1: Identify What to Test

### Must Test
- [ ] Domain entity business logic
- [ ] Command/Query handlers
- [ ] Validation rules
- [ ] Complex algorithms
- [ ] Edge cases

### Should Test
- [ ] API endpoints (integration)
- [ ] Repository queries
- [ ] External service integration

### Skip Testing
- [ ] Simple property accessors
- [ ] Framework code
- [ ] Auto-generated code

---

## Step 2: Write Unit Tests

### Test Naming Convention
```
MethodName_StateUnderTest_ExpectedBehavior
```

### AAA Pattern
```csharp
[Fact]
public void Submit_OrderWithItems_ChangesStatusToSubmitted()
{
    // Arrange - Set up the test
    var order = Order.Create(Guid.NewGuid());
    order.AddItem(Guid.NewGuid(), "Product", 10.00m, 1);
    
    // Act - Execute the code under test
    order.Submit();
    
    // Assert - Verify the result
    Assert.Equal(OrderStatus.Submitted, order.Status);
}
```

### Testing Exceptions
```csharp
[Fact]
public void Submit_EmptyOrder_ThrowsDomainException()
{
    // Arrange
    var order = Order.Create(Guid.NewGuid());
    
    // Act & Assert
    var exception = Assert.Throws<DomainException>(() => order.Submit());
    Assert.Contains("empty", exception.Message.ToLower());
}
```

### Parameterized Tests
```csharp
[Theory]
[InlineData("", false)]
[InlineData("a", false)]
[InlineData("ab", true)]
[InlineData("valid@email.com", true)]
public void Validate_Email_ReturnsExpectedResult(string email, bool expected)
{
    // Arrange
    var validator = new EmailValidator();
    
    // Act
    var result = validator.IsValid(email);
    
    // Assert
    Assert.Equal(expected, result);
}
```

---

## Step 3: Write Handler Tests

### With Mocking (Moq)
```csharp
public class CreateOrderCommandTests
{
    private readonly Mock<IOrderRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandTests()
    {
        _repositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateOrderCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesOrder()
    {
        // Arrange
        var command = new CreateOrderCommand(Guid.NewGuid());
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        Assert.NotNull(result);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

### With In-Memory Database
```csharp
public class GetOrderQueryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly GetOrderQueryHandler _handler;

    public GetOrderQueryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        _context = new ApplicationDbContext(options);
        _handler = new GetOrderQueryHandler(_context);
    }

    [Fact]
    public async Task Handle_ExistingOrder_ReturnsDto()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid());
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetOrderQuery(order.Id),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(order.Id, result.Id);
    }

    public void Dispose() => _context.Dispose();
}
```

---

## Step 4: Write Integration Tests

### WebApplicationFactory
```csharp
public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real database with in-memory
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                services.Remove(descriptor!);
                
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateOrderRequest(Guid.NewGuid());
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/orders", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
    }

    [Fact]
    public async Task GetOrder_NotFound_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

---

## Step 5: Run Tests

### Commands
```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --verbosity normal

# Run specific project
dotnet test tests/Application.UnitTests

# Run specific test
dotnet test --filter "FullyQualifiedName~CreateOrderCommandTests"

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

---

## Step 6: Test Coverage

### Generate Report
```bash
# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate coverage
dotnet test --collect:"XPlat Code Coverage"

# Create HTML report
reportgenerator \
    -reports:**/coverage.cobertura.xml \
    -targetdir:coverage-report \
    -reporttypes:Html
```

### Coverage Goals
| Layer | Target |
|-------|--------|
| Domain | 90%+ |
| Application | 80%+ |
| Infrastructure | 60%+ |
| API | 70%+ |

---

## Test Organization

```
tests/
├── Domain.UnitTests/
│   ├── Entities/
│   │   ├── OrderTests.cs
│   │   └── CustomerTests.cs
│   └── ValueObjects/
│       └── MoneyTests.cs
├── Application.UnitTests/
│   ├── Features/
│   │   └── Orders/
│   │       ├── CreateOrderCommandTests.cs
│   │       └── GetOrderQueryTests.cs
│   └── Validators/
│       └── CreateOrderValidatorTests.cs
└── Api.IntegrationTests/
    ├── OrdersApiTests.cs
    └── CustomersApiTests.cs
```

---

## Checklist
- [ ] Unit tests for domain logic
- [ ] Handler tests with mocks
- [ ] Validator tests
- [ ] Integration tests for API
- [ ] Test coverage > 80% for critical paths
- [ ] All tests pass
- [ ] Tests run in CI/CD pipeline
