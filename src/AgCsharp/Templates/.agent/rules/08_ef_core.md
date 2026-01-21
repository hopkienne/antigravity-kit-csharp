# Entity Framework Core

## DbContext Configuration

### Basic Setup
```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
        
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        // Auto-set audit fields
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### Registration in Program.cs
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly("MyApp.Infrastructure");
        });

    // Enable sensitive data logging only in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});
```

## Entity Configuration

### Fluent API Configuration
```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Table
        builder.ToTable("Customers");
        
        // Primary key
        builder.HasKey(c => c.Id);
        
        // Properties
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(c => c.Email)
            .IsRequired()
            .HasMaxLength(255);
        
        // Value Object (Owned Type)
        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(200);
            address.Property(a => a.City).HasMaxLength(100);
            address.Property(a => a.PostalCode).HasMaxLength(20);
        });
        
        // Indexes
        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
        
        // Relationships
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Customer)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

## Query Optimization

### Use Projections
```csharp
// ✅ Good - Only select needed columns
public async Task<List<CustomerDto>> GetCustomersAsync()
{
    return await _context.Customers
        .Select(c => new CustomerDto(c.Id, c.Name, c.Email))
        .ToListAsync();
}

// ❌ Bad - Loads entire entity
public async Task<List<CustomerDto>> GetCustomersAsync()
{
    var customers = await _context.Customers.ToListAsync();
    return customers.Select(c => new CustomerDto(c.Id, c.Name, c.Email)).ToList();
}
```

### Use AsNoTracking for Read-Only Queries
```csharp
// ✅ No change tracking overhead
public async Task<Customer?> GetByIdReadOnlyAsync(int id)
{
    return await _context.Customers
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == id);
}

// For DbContext-wide read-only
builder.Services.AddDbContext<ReadOnlyDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});
```

### Avoid N+1 Queries
```csharp
// ❌ N+1 Problem
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    // Each iteration executes a query
    Console.WriteLine(order.Customer.Name);
}

// ✅ Eager Loading
var orders = await _context.Orders
    .Include(o => o.Customer)
    .Include(o => o.Items)
    .ToListAsync();

// ✅ Explicit Loading (when needed conditionally)
var order = await _context.Orders.FindAsync(id);
if (order != null && needCustomer)
{
    await _context.Entry(order)
        .Reference(o => o.Customer)
        .LoadAsync();
}

// ✅ Split Query for large collections
var orders = await _context.Orders
    .Include(o => o.Items)
    .AsSplitQuery()
    .ToListAsync();
```

### Efficient Filtering
```csharp
// ✅ Filter in database
var activeCustomers = await _context.Customers
    .Where(c => c.Status == CustomerStatus.Active)
    .ToListAsync();

// ❌ Don't filter in memory
var activeCustomers = (await _context.Customers.ToListAsync())
    .Where(c => c.Status == CustomerStatus.Active)
    .ToList();
```

## Migrations

### Create Migration
```bash
# From solution root
dotnet ef migrations add InitialCreate -p src/MyApp.Infrastructure -s src/MyApp.Api

# With context specification
dotnet ef migrations add AddCustomerTable \
    --context ApplicationDbContext \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api
```

### Apply Migration
```bash
# Update database
dotnet ef database update -p src/MyApp.Infrastructure -s src/MyApp.Api

# Generate SQL script
dotnet ef migrations script -p src/MyApp.Infrastructure -s src/MyApp.Api -o migration.sql

# Generate idempotent script (safe to run multiple times)
dotnet ef migrations script --idempotent -o migration.sql
```

### Migration Best Practices
```csharp
// Custom migration for data transformation
public partial class AddFullNameColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FullName",
            table: "Customers",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        // Populate from existing data
        migrationBuilder.Sql(
            "UPDATE Customers SET FullName = FirstName + ' ' + LastName");

        // Make non-nullable after population
        migrationBuilder.AlterColumn<string>(
            name: "FullName",
            table: "Customers",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FullName", table: "Customers");
    }
}
```

## Data Seeding

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // ... other configuration

        // Seed data
        builder.HasData(
            new Customer { Id = 1, Name = "System", Email = "system@example.com" },
            new Customer { Id = 2, Name = "Admin", Email = "admin@example.com" }
        );
    }
}

// Or in DbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<CustomerStatus>().HasData(
        Enum.GetValues<CustomerStatusEnum>()
            .Select(e => new CustomerStatus 
            { 
                Id = (int)e, 
                Name = e.ToString() 
            }));
}
```

## Unit of Work Pattern

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
```

## DbContext Lifetime

```csharp
// ✅ Scoped (default) - One instance per request
builder.Services.AddDbContext<ApplicationDbContext>(options => ...);

// For background services, create scope manually
public class OrderProcessingService(IServiceScopeFactory scopeFactory)
{
    public async Task ProcessAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        
        // Use context...
    }
}
```
