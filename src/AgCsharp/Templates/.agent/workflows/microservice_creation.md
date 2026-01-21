# Workflow: Microservice Creation

## Overview
Step-by-step process for creating a new microservice.

---

## Step 1: Define Service Boundary

### Questions to Answer
- [ ] What business capability does this service own?
- [ ] What data does it manage?
- [ ] What APIs will it expose?
- [ ] What services does it depend on?
- [ ] What services depend on it?

### Service Specification
```markdown
## Service: {ServiceName}

### Business Capability
[Description of what this service does]

### Owned Data
- Entity 1
- Entity 2

### APIs
- GET /api/{resource}
- POST /api/{resource}
- ...

### Dependencies
- Service A (for X)
- Service B (for Y)

### Events Published
- {Entity}Created
- {Entity}Updated

### Events Consumed
- OrderPlaced (from Orders service)
```

---

## Step 2: Create Solution Structure

### Create Projects
```bash
# Create solution
mkdir {ServiceName}
cd {ServiceName}
dotnet new sln -n {ServiceName}

# Create projects
dotnet new webapi -n {ServiceName}.Api -o src/{ServiceName}.Api
dotnet new classlib -n {ServiceName}.Application -o src/{ServiceName}.Application
dotnet new classlib -n {ServiceName}.Domain -o src/{ServiceName}.Domain
dotnet new classlib -n {ServiceName}.Infrastructure -o src/{ServiceName}.Infrastructure

# Add to solution
dotnet sln add src/{ServiceName}.Api
dotnet sln add src/{ServiceName}.Application
dotnet sln add src/{ServiceName}.Domain
dotnet sln add src/{ServiceName}.Infrastructure

# Add project references
dotnet add src/{ServiceName}.Application reference src/{ServiceName}.Domain
dotnet add src/{ServiceName}.Infrastructure reference src/{ServiceName}.Application
dotnet add src/{ServiceName}.Api reference src/{ServiceName}.Application
dotnet add src/{ServiceName}.Api reference src/{ServiceName}.Infrastructure
```

### Folder Structure
```
{ServiceName}/
├── src/
│   ├── {ServiceName}.Api/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   └── Program.cs
│   ├── {ServiceName}.Application/
│   │   ├── Features/
│   │   ├── Common/
│   │   └── DependencyInjection.cs
│   ├── {ServiceName}.Domain/
│   │   ├── Entities/
│   │   ├── Events/
│   │   └── ValueObjects/
│   └── {ServiceName}.Infrastructure/
│       ├── Persistence/
│       └── DependencyInjection.cs
├── tests/
│   ├── {ServiceName}.UnitTests/
│   └── {ServiceName}.IntegrationTests/
├── Dockerfile
└── {ServiceName}.sln
```

---

## Step 3: Add Core Packages

```bash
# API project
dotnet add src/{ServiceName}.Api package Serilog.AspNetCore
dotnet add src/{ServiceName}.Api package Swashbuckle.AspNetCore

# Application project
dotnet add src/{ServiceName}.Application package MediatR
dotnet add src/{ServiceName}.Application package FluentValidation
dotnet add src/{ServiceName}.Application package AutoMapper.Extensions.Microsoft.DependencyInjection

# Infrastructure project
dotnet add src/{ServiceName}.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/{ServiceName}.Infrastructure package MassTransit.RabbitMQ
```

---

## Step 4: Implement Domain

```csharp
// Domain/Entities/{Entity}.cs
namespace {ServiceName}.Domain.Entities;

public class {Entity}
{
    public Guid Id { get; private set; }
    // Properties...
    
    private {Entity}() { }
    
    public static {Entity} Create(...) { }
}

// Domain/Events/{Entity}CreatedEvent.cs
public record {Entity}CreatedEvent(Guid {Entity}Id) : IDomainEvent;
```

---

## Step 5: Implement Infrastructure

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
public class ApplicationDbContext : DbContext
{
    public DbSet<{Entity}> {Entity}s => Set<{Entity}>();
    
    // ...
}

// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"]);
                cfg.ConfigureEndpoints(context);
            });
        });
        
        return services;
    }
}
```

---

## Step 6: Implement API

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

---

## Step 7: Create Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/{ServiceName}.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "{ServiceName}.Api.dll"]
```

---

## Step 8: Add to Docker Compose

```yaml
# docker-compose.yml
services:
  {servicename}:
    build:
      context: ./services/{ServiceName}
      dockerfile: Dockerfile
    ports:
      - "500X:8080"
    environment:
      - ConnectionStrings__Default=...
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - {servicename}-db
      - rabbitmq

  {servicename}-db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_DB={ServiceName}Db
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - {servicename}-db-data:/var/lib/postgresql/data

volumes:
  {servicename}-db-data:
```

---

## Step 9: API Gateway Integration

```yaml
# YARP configuration
{
  "ReverseProxy": {
    "Routes": {
      "{servicename}-route": {
        "ClusterId": "{servicename}-cluster",
        "Match": {
          "Path": "/api/{servicename}/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "{servicename}-cluster": {
        "Destinations": {
          "{servicename}-1": {
            "Address": "http://{servicename}:8080"
          }
        }
      }
    }
  }
}
```

---

## Step 10: Testing

```bash
# Run locally
docker-compose up -d {servicename}

# Test health endpoint
curl http://localhost:500X/health

# Test API
curl http://localhost:500X/api/{resource}

# Run tests
dotnet test
```

---

## Checklist

### Development
- [ ] Service boundary defined
- [ ] Solution structure created
- [ ] Domain entities implemented
- [ ] Database configured
- [ ] API endpoints created
- [ ] Message handlers implemented
- [ ] Tests written

### Deployment
- [ ] Dockerfile created
- [ ] Added to docker-compose
- [ ] API Gateway configured
- [ ] Health checks working
- [ ] Logging configured
- [ ] Documentation updated
