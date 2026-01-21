# Skill: Generate Repository

## When to Use
User requests to create a repository for data access, following the Repository pattern.

## Template

### Interface (Domain Layer)

```csharp
namespace {Namespace}.Domain.Repositories;

public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<{Entity}>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync({Entity} entity, CancellationToken ct = default);
    void Update({Entity} entity);
    void Remove({Entity} entity);
}
```

### Implementation (Infrastructure Layer)

```csharp
namespace {Namespace}.Infrastructure.Persistence.Repositories;

public class {Entity}Repository(ApplicationDbContext context) 
    : I{Entity}Repository
{
    public async Task<{Entity}?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await context.{Entities}
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }
    
    // Additional methods...
}
```

## Example Output

### Interface

```csharp
namespace MyApp.Domain.Repositories;

public interface ICustomerRepository
{
    // Basic CRUD
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    void Update(Customer customer);
    void Remove(Customer customer);
    
    // Domain-specific queries
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetActiveCustomersAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}
```

### Implementation

```csharp
namespace MyApp.Infrastructure.Persistence.Repositories;

public class CustomerRepository(ApplicationDbContext context) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await context.Customers
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Customers
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        await context.Customers.AddAsync(customer, ct);
    }

    public void Update(Customer customer)
    {
        context.Customers.Update(customer);
    }

    public void Remove(Customer customer)
    {
        context.Customers.Remove(customer);
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await context.Customers
            .FirstOrDefaultAsync(c => c.Email.Value == email.ToLowerInvariant(), ct);
    }

    public async Task<IReadOnlyList<Customer>> GetActiveCustomersAsync(CancellationToken ct = default)
    {
        return await context.Customers
            .AsNoTracking()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
    {
        return await context.Customers.AnyAsync(c => c.Id == id, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await context.Customers
            .AnyAsync(c => c.Email.Value == email.ToLowerInvariant(), ct);
    }
}
```

## Generic Repository (Optional)

```csharp
// Interface
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

// Implementation
public class Repository<T>(ApplicationDbContext context) : IRepository<T>
    where T : BaseEntity
{
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync([id], ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, 
        CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().Where(predicate).ToListAsync(ct);
    }

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
    }

    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        DbSet.Remove(entity);
    }
}
```

## Repository with Includes

```csharp
public async Task<Order?> GetWithItemsAsync(int id, CancellationToken ct = default)
{
    return await context.Orders
        .Include(o => o.Items)
        .Include(o => o.Customer)
        .FirstOrDefaultAsync(o => o.Id == id, ct);
}

public async Task<Order?> GetWithItemsAndProductsAsync(int id, CancellationToken ct = default)
{
    return await context.Orders
        .Include(o => o.Items)
            .ThenInclude(i => i.Product)
        .Include(o => o.Customer)
        .FirstOrDefaultAsync(o => o.Id == id, ct);
}
```

## DI Registration

```csharp
// In Infrastructure DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Or use generic registration
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
```

## Guidelines

1. **Interface in Domain** - Implementation in Infrastructure
2. **Async all the way** - Use async methods with CancellationToken
3. **AsNoTracking for reads** - Improve performance for read-only queries
4. **Domain-specific methods** - Add methods that match domain needs
5. **No business logic** - Repositories are for data access only
6. **Return domain entities** - Not DTOs (that's application layer concern)
