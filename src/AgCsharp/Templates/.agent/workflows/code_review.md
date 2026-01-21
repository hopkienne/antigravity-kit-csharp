# Workflow: Code Review

## Overview
Systematic checklist for reviewing code before merging to main branch.

## Pre-Review Setup

1. Pull the latest changes from the feature branch
2. Ensure the code builds successfully
3. Run all tests locally
4. Review the PR description and linked issues

---

## Code Review Checklist

### 1. Naming & Readability

- [ ] **Class names** are descriptive and use PascalCase
- [ ] **Method names** clearly describe their purpose
- [ ] **Variable names** are meaningful (avoid single letters except loops)
- [ ] **No magic numbers** - use constants or enums
- [ ] **Comments** explain "why", not "what"
- [ ] **Code is self-documenting** - minimal comments needed

```csharp
// ❌ Bad
var x = GetData();
if (x.Status == 1) { }

// ✅ Good
var customer = await GetCustomerAsync(customerId);
if (customer.Status == CustomerStatus.Active) { }
```

### 2. Architecture & Design

- [ ] **Single Responsibility** - each class/method does one thing
- [ ] **Dependency Injection** - no `new` for services
- [ ] **Layer boundaries** - no infrastructure in domain
- [ ] **No circular dependencies**
- [ ] **Proper abstraction level** - not over-engineered

```csharp
// ❌ Bad - Repository in Controller
public class OrderController
{
    private readonly ApplicationDbContext _context; // Direct DB access
}

// ✅ Good - Proper layering
public class OrderController
{
    private readonly ISender _sender; // Uses MediatR
}
```

### 3. Error Handling

- [ ] **No empty catch blocks**
- [ ] **Specific exceptions** caught before general
- [ ] **Errors logged** with appropriate level
- [ ] **Result pattern** used instead of exceptions for expected failures
- [ ] **Validation** at boundaries

```csharp
// ❌ Bad
try { } catch { } // Silent failure

// ✅ Good
try
{
    await ProcessOrderAsync(order);
}
catch (ExternalServiceException ex)
{
    _logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
    throw;
}
```

### 4. Security

- [ ] **No SQL injection** - parameterized queries only
- [ ] **No sensitive data in logs** (passwords, tokens, PII)
- [ ] **Authorization checks** on endpoints
- [ ] **Input validation** on all user input
- [ ] **No hardcoded secrets**

```csharp
// ❌ Bad - SQL Injection risk
var sql = $"SELECT * FROM Users WHERE Email = '{email}'";

// ✅ Good
var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
```

### 5. Performance

- [ ] **Async/await** used correctly for I/O
- [ ] **No N+1 queries** - use Include() or projection
- [ ] **AsNoTracking()** for read-only queries
- [ ] **Proper indexing** considered for new columns
- [ ] **No blocking calls** (.Result, .Wait())

```csharp
// ❌ Bad - N+1 query
foreach (var order in orders)
{
    var customer = await _context.Customers.FindAsync(order.CustomerId);
}

// ✅ Good - Eager loading
var orders = await _context.Orders.Include(o => o.Customer).ToListAsync();
```

### 6. Testing

- [ ] **Unit tests** for business logic
- [ ] **Tests follow AAA pattern** (Arrange, Act, Assert)
- [ ] **Edge cases covered** (null, empty, boundaries)
- [ ] **Test names** are descriptive
- [ ] **No test dependencies** on other tests

```csharp
// ✅ Good test structure
[Fact]
public async Task CreateOrder_ShouldFail_WhenCustomerIsInactive()
{
    // Arrange
    var customer = CreateInactiveCustomer();
    
    // Act
    var result = await _service.CreateOrderAsync(customer.Id, items);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Should().Contain("inactive");
}
```

### 7. API Design

- [ ] **Correct HTTP methods** (GET for reads, POST for creates, etc.)
- [ ] **Appropriate status codes** returned
- [ ] **Consistent response format** (ProblemDetails for errors)
- [ ] **Versioning** if breaking changes
- [ ] **OpenAPI documentation** updated

### 8. Database

- [ ] **Migrations** are correct and reversible
- [ ] **No breaking changes** to existing data
- [ ] **Indexes** added for frequently queried columns
- [ ] **Foreign keys** properly configured
- [ ] **Data types** appropriate for the data

---

## Review Comments Guide

### Severity Levels

| Level | Description | Action Required |
|-------|-------------|-----------------|
| 🔴 **Blocker** | Security issue, data loss risk, build break | Must fix before merge |
| 🟠 **Major** | Bug, missing validation, incorrect logic | Should fix before merge |
| 🟡 **Minor** | Code style, naming, small improvements | Can fix or discuss |
| 🟢 **Nitpick** | Personal preference, optional | Nice to have |
| 💡 **Suggestion** | Alternative approach, learning | For discussion |

### Comment Templates

```markdown
🔴 **BLOCKER**: SQL Injection vulnerability
This query is vulnerable to SQL injection. Use parameterized queries.

🟠 **MAJOR**: Missing null check
`customer` can be null here but isn't checked before accessing properties.

🟡 **MINOR**: Consider renaming
`ProcessData` doesn't clearly describe what this method does. 
Consider `CalculateOrderTotal`.

💡 **SUGGESTION**: Alternative approach
Consider using the specification pattern here for more flexible querying.
```

---

## Post-Review

1. **Respond to all comments** - resolve or discuss
2. **Request re-review** after changes
3. **Squash commits** if needed before merge
4. **Update documentation** if behavior changed
5. **Notify team** of significant changes

---

## Approval Criteria

A PR can be approved when:

- [ ] All blockers are resolved
- [ ] All major issues are fixed
- [ ] Tests pass in CI/CD
- [ ] At least one approval from team member
- [ ] No merge conflicts
- [ ] Documentation is updated (if needed)
