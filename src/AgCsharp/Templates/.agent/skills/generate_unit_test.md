# Skill: Generate Unit Test

## When to Use
User requests to create unit tests, test cases, or test coverage for a class or method.

## Template

```csharp
namespace {Namespace}.Tests.Unit;

public class {ClassName}Tests
{
    private readonly Mock<I{Dependency}> _{dependency}Mock;
    private readonly {ClassName} _sut; // System Under Test

    public {ClassName}Tests()
    {
        _{dependency}Mock = new Mock<I{Dependency}>();
        _sut = new {ClassName}(_{dependency}Mock.Object);
    }

    [Fact]
    public async Task {MethodName}_Should{ExpectedBehavior}_When{Condition}()
    {
        // Arrange
        
        // Act
        
        // Assert
    }
}
```

## Example Output

### Service Tests

```csharp
namespace MyApp.Tests.Unit.Services;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CustomerService>> _loggerMock;
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _repositoryMock = new Mock<ICustomerRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CustomerService>>();
        
        _sut = new CustomerService(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCustomer_WhenCustomerExists()
    {
        // Arrange
        var customerId = 1;
        var customer = CreateTestCustomer(customerId, "John Doe", "john@example.com");
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _sut.GetByIdAsync(customerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(customerId);
        result.Value.Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFailure_WhenCustomerNotFound()
    {
        // Arrange
        var customerId = 999;
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        // Act
        var result = await _sut.GetByIdAsync(customerId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateCustomer_WhenEmailIsUnique()
    {
        // Arrange
        var request = new CreateCustomerRequest("John Doe", "john@example.com");
        
        _repositoryMock
            .Setup(x => x.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        _repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _unitOfWorkMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        _repositoryMock.Verify(
            x => x.AddAsync(It.Is<Customer>(c => 
                c.Name == request.Name && 
                c.Email.Value == request.Email.ToLowerInvariant()), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
        
        _unitOfWorkMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFailure_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new CreateCustomerRequest("John Doe", "existing@example.com");
        
        _repositoryMock
            .Setup(x => x.EmailExistsAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
        
        _repositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-email")]
    public async Task CreateAsync_ShouldReturnFailure_WhenEmailIsInvalid(string invalidEmail)
    {
        // Arrange
        var request = new CreateCustomerRequest("John Doe", invalidEmail);
        
        _repositoryMock
            .Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region DeactivateAsync Tests

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateCustomer_WhenCustomerIsActive()
    {
        // Arrange
        var customerId = 1;
        var customer = CreateTestCustomer(customerId, "John Doe", "john@example.com");
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _sut.DeactivateAsync(customerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        customer.Status.Should().Be(CustomerStatus.Inactive);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldReturnFailure_WhenCustomerAlreadyInactive()
    {
        // Arrange
        var customerId = 1;
        var customer = CreateTestCustomer(customerId, "John Doe", "john@example.com");
        customer.Deactivate(); // Already inactive
        
        _repositoryMock
            .Setup(x => x.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        // Act
        var result = await _sut.DeactivateAsync(customerId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already inactive");
    }

    #endregion

    #region Test Helpers

    private static Customer CreateTestCustomer(int id, string name, string email)
    {
        // Use reflection or factory method to create test customer
        var customer = Customer.Create(name, Email.Create(email).Value);
        
        // Set ID using reflection if needed
        typeof(Customer)
            .GetProperty(nameof(Customer.Id))!
            .SetValue(customer, id);
        
        return customer;
    }

    #endregion
}
```

### Entity Tests

```csharp
namespace MyApp.Tests.Unit.Domain;

public class CustomerTests
{
    [Fact]
    public void Create_ShouldCreateActiveCustomer_WithValidData()
    {
        // Arrange
        var name = "John Doe";
        var email = Email.Create("john@example.com").Value;

        // Act
        var customer = Customer.Create(name, email);

        // Assert
        customer.Name.Should().Be(name);
        customer.Email.Should().Be(email);
        customer.Status.Should().Be(CustomerStatus.Active);
        customer.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CustomerCreatedEvent>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowException_WhenNameIsInvalid(string? invalidName)
    {
        // Arrange
        var email = Email.Create("john@example.com").Value;

        // Act
        var act = () => Customer.Create(invalidName!, email);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ShouldSetStatusToInactive_WhenActive()
    {
        // Arrange
        var customer = CreateActiveCustomer();

        // Act
        customer.Deactivate();

        // Assert
        customer.Status.Should().Be(CustomerStatus.Inactive);
        customer.DeactivatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_ShouldThrowDomainException_WhenAlreadyInactive()
    {
        // Arrange
        var customer = CreateActiveCustomer();
        customer.Deactivate();

        // Act
        var act = () => customer.Deactivate();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*already inactive*");
    }

    private static Customer CreateActiveCustomer()
    {
        return Customer.Create("Test", Email.Create("test@example.com").Value);
    }
}
```

### Value Object Tests

```csharp
namespace MyApp.Tests.Unit.Domain.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.org")]
    [InlineData("email@subdomain.domain.com")]
    public void Create_ShouldSucceed_WithValidEmail(string validEmail)
    {
        // Act
        var result = Email.Create(validEmail);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(validEmail.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    public void Create_ShouldFail_WithInvalidEmail(string invalidEmail)
    {
        // Act
        var result = Email.Create(invalidEmail);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForSameEmail()
    {
        // Arrange
        var email1 = Email.Create("test@example.com").Value;
        var email2 = Email.Create("TEST@EXAMPLE.COM").Value;

        // Assert
        email1.Should().Be(email2);
    }
}
```

## Guidelines

1. **AAA Pattern** - Arrange, Act, Assert
2. **One assertion per test** - When possible
3. **Descriptive names** - MethodName_Should_When format
4. **Test edge cases** - null, empty, boundaries
5. **Use Theory for data-driven tests**
6. **Mock dependencies** - Isolate unit under test
7. **FluentAssertions** - Readable assertions
