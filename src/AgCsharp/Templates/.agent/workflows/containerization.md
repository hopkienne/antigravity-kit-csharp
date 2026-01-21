# Workflow: Containerization

## Overview
Step-by-step process for containerizing a .NET application.

---

## Step 1: Prepare Application

### Health Endpoints
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "database", tags: ["ready"]);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });
```

### Configuration for Docker
```csharp
// Allow configuration from environment variables
builder.Configuration.AddEnvironmentVariables();

// Configure Kestrel for container
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});
```

### Logging Configuration
```csharp
// Console logging for Docker
builder.Logging.AddConsole();
builder.Logging.AddJsonConsole(); // Structured for log aggregators
```

---

## Step 2: Create Dockerfile

### Multi-Stage Dockerfile
```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
COPY ["src/MyApp.Application/MyApp.Application.csproj", "src/MyApp.Application/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["src/MyApp.Infrastructure/MyApp.Infrastructure.csproj", "src/MyApp.Infrastructure/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

# Copy everything and build
COPY . .
RUN dotnet publish "src/MyApp.Api/MyApp.Api.csproj" -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Security: Non-root user
RUN addgroup --gid 1000 appgroup \
    && adduser --uid 1000 --gid 1000 --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

---

## Step 3: Create .dockerignore

```dockerignore
# .dockerignore
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/node_modules
**/.idea
**/Dockerfile*
**/docker-compose*
**/*.md
**/*.log
**/tests
**/*.Tests
.env*
*.user
.DS_Store
```

---

## Step 4: Build and Test Image

```bash
# Build image
docker build -t myapp:latest .

# Run container
docker run -d \
    -p 5000:8080 \
    -e ASPNETCORE_ENVIRONMENT=Development \
    -e ConnectionStrings__Default="Server=host.docker.internal;..." \
    --name myapp \
    myapp:latest

# Check logs
docker logs myapp

# Test health endpoint
curl http://localhost:5000/health/live

# Stop and remove
docker stop myapp && docker rm myapp
```

---

## Step 5: Create Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Server=db;Database=MyAppDb;User=sa;Password=${DB_PASSWORD};TrustServerCertificate=true
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
    networks:
      - app-network

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${DB_PASSWORD}
    ports:
      - "1433:1433"
    volumes:
      - db-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "${DB_PASSWORD}" -Q "SELECT 1"
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 30s
    networks:
      - app-network

networks:
  app-network:
    driver: bridge

volumes:
  db-data:
```

### Environment File
```bash
# .env
DB_PASSWORD=YourStrong@Password123
```

---

## Step 6: Local Testing

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f api

# Check service status
docker-compose ps

# Test API
curl http://localhost:5000/api/health

# Stop services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

---

## Step 7: Optimize Image

### Check Image Size
```bash
docker images myapp
```

### Optimization Options

#### Option 1: Alpine Base
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
# Smaller but may have compatibility issues
```

#### Option 2: Chiseled Image (Smallest)
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled AS final
# Requires self-contained publish
```

#### Option 3: Multi-Platform
```dockerfile
# Build for multiple platforms
docker buildx build --platform linux/amd64,linux/arm64 -t myapp:latest .
```

---

## Step 8: Push to Registry

```bash
# Tag image
docker tag myapp:latest registry.example.com/myapp:latest
docker tag myapp:latest registry.example.com/myapp:1.0.0

# Push to registry
docker push registry.example.com/myapp:latest
docker push registry.example.com/myapp:1.0.0

# Or use Docker Hub
docker tag myapp:latest username/myapp:latest
docker push username/myapp:latest
```

---

## Checklist

### Dockerfile
- [ ] Multi-stage build
- [ ] Layer caching optimized
- [ ] Non-root user
- [ ] .dockerignore created
- [ ] Health check defined

### Application
- [ ] Health endpoints configured
- [ ] Environment variable configuration
- [ ] Proper logging for containers

### Docker Compose
- [ ] All dependencies defined
- [ ] Health checks with conditions
- [ ] Volumes for persistence
- [ ] Network configured

### Testing
- [ ] Image builds successfully
- [ ] Container runs and serves requests
- [ ] Health checks pass
- [ ] Can connect to dependencies

### Deployment
- [ ] Image pushed to registry
- [ ] Image tagged with version
- [ ] Documentation updated
