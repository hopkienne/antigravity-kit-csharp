# Workflow: Debugging

## Overview
Systematic process for diagnosing and fixing issues in production or development.

---

## Step 1: Gather Information

### Read the Error/Symptom
- What is the exact error message?
- When did it start happening?
- Is it reproducible? How often?
- What changed recently? (deployments, config changes)

### Collect Evidence
```
□ Exception message and type
□ Stack trace (full, not truncated)
□ Request details (URL, method, body)
□ User context (who, when, from where)
□ Environment (dev, staging, prod)
□ Correlation ID / Request ID
□ Recent logs around the time of error
```

---

## Step 2: Read the Stack Trace

### Anatomy of a Stack Trace

```
System.InvalidOperationException: Sequence contains no elements
   at System.Linq.ThrowHelper.ThrowNoElementsException()
   at System.Linq.Enumerable.First[TSource](IEnumerable`1 source)
   at MyApp.Application.Services.OrderService.GetLatestOrderAsync(Int32 customerId) in OrderService.cs:line 45
   at MyApp.Api.Controllers.OrdersController.GetLatest(Int32 customerId) in OrdersController.cs:line 28
```

### How to Read It
1. **Exception Type**: `InvalidOperationException` - tells you the category
2. **Message**: `Sequence contains no elements` - the specific issue
3. **Top of stack**: Where the exception was thrown (Linq.First)
4. **Your code**: First line in your namespace (OrderService.cs:line 45)
5. **Entry point**: How it got there (Controller)

### Common Exception Types

| Exception | Typical Cause |
|-----------|--------------|
| `NullReferenceException` | Accessing member on null object |
| `InvalidOperationException` | Operation invalid for current state |
| `ArgumentException` | Invalid argument passed |
| `DbUpdateException` | Database constraint violation |
| `HttpRequestException` | External API call failed |
| `TimeoutException` | Operation took too long |

---

## Step 3: Identify the Layer

### Where is the problem?

```
┌─────────────────────────────────────────┐
│             Presentation                 │
│  Controllers, Middleware, Filters        │
│  → Check: Request/Response, Auth         │
├─────────────────────────────────────────┤
│             Application                  │
│  Handlers, Services, Validators          │
│  → Check: Business logic, Validation     │
├─────────────────────────────────────────┤
│               Domain                     │
│  Entities, Value Objects, Events         │
│  → Check: Invariants, State transitions  │
├─────────────────────────────────────────┤
│            Infrastructure                │
│  Database, External APIs, Files          │
│  → Check: Connections, Queries, Timeouts │
└─────────────────────────────────────────┘
```

---

## Step 4: Reproduce the Issue

### Create Minimal Reproduction

```csharp
// Write a failing test that reproduces the issue
[Fact]
public async Task GetLatestOrder_ShouldThrow_WhenCustomerHasNoOrders()
{
    // Arrange
    var customerId = 123; // Customer with no orders
    
    // Act
    var act = () => _service.GetLatestOrderAsync(customerId);
    
    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>();
}
```

### Check Different Scenarios
- Does it happen with all data or specific data?
- Does it happen for all users or specific users?
- Is it time-dependent (batch jobs, scheduled tasks)?
- Is it load-dependent (high traffic only)?

---

## Step 5: Analyze the Code

### Common Bug Patterns

#### Null Reference
```csharp
// Problem
var customer = await _repo.GetByIdAsync(id);
var name = customer.Name; // 💥 if customer is null

// Solution
var customer = await _repo.GetByIdAsync(id);
if (customer is null)
    return Result.Failure("Customer not found");
var name = customer.Name;
```

#### Empty Collection
```csharp
// Problem
var latestOrder = orders.First(); // 💥 if empty

// Solution
var latestOrder = orders.FirstOrDefault();
if (latestOrder is null)
    return Result.Failure("No orders found");
```

#### Async Deadlock
```csharp
// Problem (in non-async context)
var result = GetDataAsync().Result; // 💥 deadlock risk

// Solution
var result = await GetDataAsync();
```

#### Race Condition
```csharp
// Problem
if (await _repo.ExistsAsync(email))
    throw new Exception("Email exists");
await _repo.CreateAsync(user); // 💥 another request might create between check and create

// Solution - Use unique constraint + catch exception
// Or use transaction with proper isolation
```

---

## Step 6: Fix the Issue

### Apply the Fix
1. Write a failing test first (TDD)
2. Make the minimal change to fix the issue
3. Verify the test passes
4. Check for similar issues elsewhere

### Example Fix

```csharp
// Before (buggy)
public async Task<Order> GetLatestOrderAsync(int customerId)
{
    var orders = await _context.Orders
        .Where(o => o.CustomerId == customerId)
        .OrderByDescending(o => o.CreatedAt)
        .ToListAsync();
    
    return orders.First(); // 💥 throws if no orders
}

// After (fixed)
public async Task<Order?> GetLatestOrderAsync(int customerId)
{
    return await _context.Orders
        .Where(o => o.CustomerId == customerId)
        .OrderByDescending(o => o.CreatedAt)
        .FirstOrDefaultAsync(); // Returns null if no orders
}
```

---

## Step 7: Verify the Fix

### Testing Checklist
- [ ] Failing test now passes
- [ ] Existing tests still pass
- [ ] Manual testing confirms fix
- [ ] Edge cases covered
- [ ] No regression introduced

### Log Analysis
- Verify errors stop appearing
- Check performance hasn't degraded
- Monitor for new/related errors

---

## Step 8: Prevent Recurrence

### Add Guards
```csharp
// Add validation
ArgumentNullException.ThrowIfNull(customer);

// Add defensive checks
if (!orders.Any())
{
    _logger.LogWarning("No orders found for customer {CustomerId}", customerId);
    return null;
}
```

### Add Monitoring
```csharp
// Add metrics
_metrics.IncrementCounter("orders.notfound", tags: new { customerId });

// Add alerting rules
// Alert if orders.notfound > 100 in 5 minutes
```

### Document the Issue
- Add comments explaining the fix
- Update runbook if operational issue
- Create knowledge base article if common

---

## Debugging Tools

### Logging
```csharp
// Add structured logging at key points
_logger.LogDebug("Fetching orders for customer {CustomerId}", customerId);
_logger.LogInformation("Found {OrderCount} orders", orders.Count);
_logger.LogWarning("No orders found for customer {CustomerId}", customerId);
```

### Application Insights / Seq
- Search by correlation ID
- View request timeline
- Check dependency calls

### SQL Profiler
- Capture actual queries
- Check execution plans
- Identify slow queries

### Debugger
- Set breakpoints
- Watch variables
- Step through code
