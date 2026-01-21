# Skill: Refactor to Clean Code

## When to Use
User requests to clean up, refactor, or improve existing code quality.

## Refactoring Patterns

### 1. Extract Method

**Before:**
```csharp
public async Task<Order> ProcessOrderAsync(CreateOrderRequest request)
{
    // Validate customer
    var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
    if (customer == null)
        throw new NotFoundException("Customer not found");
    if (customer.Status != CustomerStatus.Active)
        throw new BusinessException("Customer is not active");
    if (customer.CreditLimit < request.Total)
        throw new BusinessException("Insufficient credit limit");

    // Calculate totals
    decimal subtotal = 0;
    foreach (var item in request.Items)
    {
        var product = await _productRepository.GetByIdAsync(item.ProductId);
        subtotal += product.Price * item.Quantity;
    }
    var tax = subtotal * 0.1m;
    var total = subtotal + tax;

    // Create order
    var order = new Order
    {
        CustomerId = customer.Id,
        Subtotal = subtotal,
        Tax = tax,
        Total = total,
        Status = OrderStatus.Pending
    };
    
    return order;
}
```

**After:**
```csharp
public async Task<Order> ProcessOrderAsync(CreateOrderRequest request)
{
    var customer = await ValidateCustomerAsync(request.CustomerId, request.Total);
    var (subtotal, tax, total) = await CalculateOrderTotalsAsync(request.Items);
    
    return Order.Create(customer.Id, subtotal, tax, total);
}

private async Task<Customer> ValidateCustomerAsync(int customerId, decimal orderTotal)
{
    var customer = await _customerRepository.GetByIdAsync(customerId)
        ?? throw new NotFoundException("Customer not found");
    
    if (customer.Status != CustomerStatus.Active)
        throw new BusinessException("Customer is not active");
    
    if (customer.CreditLimit < orderTotal)
        throw new BusinessException("Insufficient credit limit");
    
    return customer;
}

private async Task<(decimal Subtotal, decimal Tax, decimal Total)> CalculateOrderTotalsAsync(
    IEnumerable<OrderItemRequest> items)
{
    var subtotal = 0m;
    
    foreach (var item in items)
    {
        var product = await _productRepository.GetByIdAsync(item.ProductId);
        subtotal += product.Price * item.Quantity;
    }
    
    var tax = subtotal * TaxRate;
    return (subtotal, tax, subtotal + tax);
}

private const decimal TaxRate = 0.1m;
```

### 2. Replace Magic Numbers with Constants

**Before:**
```csharp
if (retryCount > 3)
    throw new Exception("Too many retries");

if (password.Length < 8)
    return false;

Thread.Sleep(5000);
```

**After:**
```csharp
private const int MaxRetryAttempts = 3;
private const int MinPasswordLength = 8;
private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

if (retryCount > MaxRetryAttempts)
    throw new MaxRetriesExceededException();

if (password.Length < MinPasswordLength)
    return false;

await Task.Delay(RetryDelay);
```

### 3. Replace Conditional with Polymorphism

**Before:**
```csharp
public decimal CalculateShipping(Order order)
{
    switch (order.ShippingType)
    {
        case ShippingType.Standard:
            return order.Weight * 5.0m;
        case ShippingType.Express:
            return order.Weight * 10.0m + 15.0m;
        case ShippingType.Overnight:
            return order.Weight * 15.0m + 30.0m;
        default:
            throw new ArgumentException("Unknown shipping type");
    }
}
```

**After:**
```csharp
public interface IShippingCalculator
{
    decimal Calculate(Order order);
}

public class StandardShipping : IShippingCalculator
{
    public decimal Calculate(Order order) => order.Weight * 5.0m;
}

public class ExpressShipping : IShippingCalculator
{
    public decimal Calculate(Order order) => order.Weight * 10.0m + 15.0m;
}

public class OvernightShipping : IShippingCalculator
{
    public decimal Calculate(Order order) => order.Weight * 15.0m + 30.0m;
}

// Usage with DI
public class ShippingService(IEnumerable<IShippingCalculator> calculators)
{
    public decimal CalculateShipping(Order order)
    {
        var calculator = calculators.First(c => c.CanHandle(order.ShippingType));
        return calculator.Calculate(order);
    }
}
```

### 4. Guard Clauses

**Before:**
```csharp
public void ProcessPayment(Payment payment)
{
    if (payment != null)
    {
        if (payment.Amount > 0)
        {
            if (payment.CardNumber != null)
            {
                // Process payment
                _paymentGateway.Charge(payment);
            }
            else
            {
                throw new ArgumentException("Card number is required");
            }
        }
        else
        {
            throw new ArgumentException("Amount must be positive");
        }
    }
    else
    {
        throw new ArgumentNullException(nameof(payment));
    }
}
```

**After:**
```csharp
public void ProcessPayment(Payment payment)
{
    ArgumentNullException.ThrowIfNull(payment);
    
    if (payment.Amount <= 0)
        throw new ArgumentException("Amount must be positive", nameof(payment));
    
    if (string.IsNullOrEmpty(payment.CardNumber))
        throw new ArgumentException("Card number is required", nameof(payment));
    
    _paymentGateway.Charge(payment);
}
```

### 5. Introduce Parameter Object

**Before:**
```csharp
public async Task<Report> GenerateReportAsync(
    DateTime startDate,
    DateTime endDate,
    string department,
    bool includeDetails,
    ReportFormat format,
    string title,
    bool sendEmail,
    string emailRecipient)
{
    // Generate report
}
```

**After:**
```csharp
public record GenerateReportRequest(
    DateTime StartDate,
    DateTime EndDate,
    string Department,
    bool IncludeDetails = false,
    ReportFormat Format = ReportFormat.Pdf,
    string? Title = null,
    EmailOptions? Email = null);

public record EmailOptions(string Recipient, bool SendOnComplete = true);

public async Task<Report> GenerateReportAsync(GenerateReportRequest request)
{
    // Generate report
}
```

### 6. Replace Nested Conditionals with LINQ

**Before:**
```csharp
public List<Customer> GetPremiumCustomers(List<Customer> customers)
{
    var result = new List<Customer>();
    
    foreach (var customer in customers)
    {
        if (customer.Status == CustomerStatus.Active)
        {
            if (customer.TotalPurchases > 10000)
            {
                if (customer.AccountAge.TotalDays > 365)
                {
                    result.Add(customer);
                }
            }
        }
    }
    
    return result;
}
```

**After:**
```csharp
public List<Customer> GetPremiumCustomers(List<Customer> customers)
{
    return customers
        .Where(c => c.Status == CustomerStatus.Active)
        .Where(c => c.TotalPurchases > PremiumThreshold)
        .Where(c => c.AccountAge > MinAccountAge)
        .ToList();
}

private const decimal PremiumThreshold = 10000m;
private static readonly TimeSpan MinAccountAge = TimeSpan.FromDays(365);
```

### 7. Use Record Types for DTOs

**Before:**
```csharp
public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    
    public override bool Equals(object obj)
    {
        if (obj is CustomerDto other)
        {
            return Id == other.Id && 
                   Name == other.Name && 
                   Email == other.Email;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Email);
    }
}
```

**After:**
```csharp
public record CustomerDto(int Id, string Name, string Email);
```

### 8. Expression-Bodied Members

**Before:**
```csharp
public class Customer
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    public string FullName
    {
        get
        {
            return $"{FirstName} {LastName}";
        }
    }
    
    public bool IsActive()
    {
        return Status == CustomerStatus.Active;
    }
    
    public override string ToString()
    {
        return $"Customer: {FullName}";
    }
}
```

**After:**
```csharp
public class Customer
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    public string FullName => $"{FirstName} {LastName}";
    
    public bool IsActive() => Status == CustomerStatus.Active;
    
    public override string ToString() => $"Customer: {FullName}";
}
```

## SOLID Principles Checklist

1. **Single Responsibility** - Does this class have only one reason to change?
2. **Open/Closed** - Can behavior be extended without modifying code?
3. **Liskov Substitution** - Can derived classes be used in place of base?
4. **Interface Segregation** - Are interfaces focused and minimal?
5. **Dependency Inversion** - Do we depend on abstractions, not concretions?

## Code Smells to Look For

- Long methods (> 20 lines)
- Large classes (> 200 lines)
- Deep nesting (> 3 levels)
- Magic numbers/strings
- Duplicate code
- Long parameter lists (> 3 parameters)
- Feature envy (using another class's data extensively)
- Comments explaining what (not why)
