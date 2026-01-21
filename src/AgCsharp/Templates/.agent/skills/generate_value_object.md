# Skill: Generate Value Object

## When to Use
User requests to create a DDD Value Object - an immutable object defined by its attributes.

## Template

```csharp
namespace {Namespace}.Domain.ValueObjects;

public class {ValueObjectName} : ValueObject
{
    public {PropertyType} {PropertyName} { get; }
    
    private {ValueObjectName}({parameters})
    {
        {PropertyName} = {value};
    }
    
    public static Result<{ValueObjectName}> Create({parameters})
    {
        // Validation
        if (/* invalid */)
            return Result.Failure<{ValueObjectName}>("Error message");
        
        return Result.Success(new {ValueObjectName}({parameters}));
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return {PropertyName};
    }
}
```

## Example Output

### Email Value Object

```csharp
namespace MyApp.Domain.ValueObjects;

public class Email : ValueObject
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static Result<Email> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<Email>("Email cannot be empty");

        email = email.Trim();

        if (email.Length > 255)
            return Result.Failure<Email>("Email cannot exceed 255 characters");

        if (!IsValidFormat(email))
            return Result.Failure<Email>("Invalid email format");

        return Result.Success(new Email(email));
    }

    private static bool IsValidFormat(string email)
    {
        return Regex.IsMatch(email, 
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$", 
            RegexOptions.IgnoreCase);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
}
```

### Money Value Object

```csharp
namespace MyApp.Domain.ValueObjects;

public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be 3-letter ISO code", nameof(currency));

        return new Money(amount, currency);
    }

    public static Money Zero => new(0, "USD");

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        var result = left.Amount - right.Amount;
        
        if (result < 0)
            throw new InvalidOperationException("Result cannot be negative");
        
        return new Money(result, left.Currency);
    }

    public static Money operator *(Money money, int quantity)
    {
        return new Money(money.Amount * quantity, money.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException(
                $"Cannot operate on different currencies: {left.Currency} and {right.Currency}");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
```

### Address Value Object

```csharp
namespace MyApp.Domain.ValueObjects;

public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string state, 
                    string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static Result<Address> Create(
        string street, 
        string city, 
        string state, 
        string postalCode, 
        string country)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(street))
            errors.Add("Street is required");

        if (string.IsNullOrWhiteSpace(city))
            errors.Add("City is required");

        if (string.IsNullOrWhiteSpace(state))
            errors.Add("State is required");

        if (string.IsNullOrWhiteSpace(postalCode))
            errors.Add("Postal code is required");

        if (string.IsNullOrWhiteSpace(country))
            errors.Add("Country is required");

        if (errors.Any())
            return Result.Failure<Address>(string.Join(", ", errors));

        return Result.Success(new Address(
            street.Trim(),
            city.Trim(),
            state.Trim(),
            postalCode.Trim(),
            country.Trim().ToUpperInvariant()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() => 
        $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
```

### DateRange Value Object

```csharp
namespace MyApp.Domain.ValueObjects;

public class DateRange : ValueObject
{
    public DateTime Start { get; }
    public DateTime End { get; }
    public int DurationInDays => (End - Start).Days;

    private DateRange(DateTime start, DateTime end)
    {
        Start = start.Date;
        End = end.Date;
    }

    public static Result<DateRange> Create(DateTime start, DateTime end)
    {
        if (end < start)
            return Result.Failure<DateRange>("End date must be after start date");

        return Result.Success(new DateRange(start, end));
    }

    public bool Overlaps(DateRange other)
    {
        return Start < other.End && other.Start < End;
    }

    public bool Contains(DateTime date)
    {
        return date >= Start && date <= End;
    }

    public DateRange? GetOverlap(DateRange other)
    {
        if (!Overlaps(other))
            return null;

        var overlapStart = Start > other.Start ? Start : other.Start;
        var overlapEnd = End < other.End ? End : other.End;

        return new DateRange(overlapStart, overlapEnd);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }

    public override string ToString() => $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd}";
}
```

### Base Value Object Class

```csharp
namespace MyApp.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
```

### EF Core Configuration

```csharp
// As Owned Type
builder.OwnsOne(c => c.Email, email =>
{
    email.Property(e => e.Value)
        .HasColumnName("Email")
        .HasMaxLength(255)
        .IsRequired();
});

// Complex type (EF Core 8+)
builder.ComplexProperty(c => c.Address, address =>
{
    address.Property(a => a.Street).HasMaxLength(200);
    address.Property(a => a.City).HasMaxLength(100);
    address.Property(a => a.State).HasMaxLength(50);
    address.Property(a => a.PostalCode).HasMaxLength(20);
    address.Property(a => a.Country).HasMaxLength(2);
});
```

## Guidelines

1. **Immutability** - All properties are init-only or constructor-set
2. **Self-validation** - Validate in factory method, return Result
3. **Value equality** - Override Equals/GetHashCode based on properties
4. **No identity** - Value objects have no ID
5. **Side-effect free** - Methods return new instances, don't mutate
6. **Meaningful operations** - Add operators for domain operations
