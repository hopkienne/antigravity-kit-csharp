# Docker & Containerization

## Multi-Stage Dockerfile

### ASP.NET Core Application

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
COPY ["src/MyApp.Application/MyApp.Application.csproj", "src/MyApp.Application/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["src/MyApp.Infrastructure/MyApp.Infrastructure.csproj", "src/MyApp.Infrastructure/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/MyApp.Api"
RUN dotnet build "MyApp.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "MyApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Security: Run as non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy published output
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

### .NET 8 Chiseled Image (Minimal)

```dockerfile
# Using chiseled image for smaller size and attack surface
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

COPY . .
WORKDIR "/src/src/MyApp.Api"
RUN dotnet publish "MyApp.Api.csproj" -c Release -o /app/publish \
    --self-contained true \
    -r linux-x64 \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true

# Chiseled image - no shell, no root user
FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["./MyApp.Api"]
```

## Image Optimization

### Layer Caching Best Practices

```dockerfile
# ✅ Good - Dependencies change less often than source code
COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet build

# ❌ Bad - Any change invalidates cache
COPY . .
RUN dotnet restore
RUN dotnet build
```

### .dockerignore

```dockerignore
# Git
.git
.gitignore

# Build outputs
**/bin
**/obj
**/out

# IDE
.vs
.vscode
*.user
*.suo

# Docker
**/Dockerfile*
**/docker-compose*

# Tests (if not needed in image)
**/*Tests*

# Documentation
*.md
LICENSE

# Local settings
**/appsettings.Development.json
**/appsettings.Local.json
```

## Security Best Practices

### Non-Root User

```dockerfile
# Create non-root user
RUN addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup appuser

# Change ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser
```

### Read-Only Root Filesystem

```yaml
# docker-compose.yml
services:
  api:
    image: myapp:latest
    read_only: true
    tmpfs:
      - /tmp
    volumes:
      - type: tmpfs
        target: /app/logs
```

### Secrets Management

```dockerfile
# ❌ Never do this - secrets in build
ENV CONNECTION_STRING="Server=..."
COPY secrets.json /app/

# ✅ Use build secrets (BuildKit)
RUN --mount=type=secret,id=nuget_config,target=/root/.nuget/NuGet/NuGet.Config \
    dotnet restore

# ✅ Runtime secrets via environment or volume
# docker run -e ConnectionString="..." myapp
# docker run -v /secrets:/app/secrets:ro myapp
```

## Docker Compose for Development

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: src/MyApp.Api/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Server=db;Database=MyApp;User=sa;Password=YourStrong@Password;TrustServerCertificate=true
      - Redis__Connection=redis:6379
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_started
    networks:
      - myapp-network

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Password
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$$MSSQL_SA_PASSWORD" -Q "SELECT 1"
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 10s
    networks:
      - myapp-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - myapp-network

  seq:
    image: datalust/seq:latest
    environment:
      - ACCEPT_EULA=Y
    ports:
      - "5341:80"
    volumes:
      - seq-data:/data
    networks:
      - myapp-network

volumes:
  sqlserver-data:
  redis-data:
  seq-data:

networks:
  myapp-network:
    driver: bridge
```

## Docker Compose Override for Development

```yaml
# docker-compose.override.yml
version: '3.8'

services:
  api:
    build:
      target: build  # Use build stage for hot reload
    volumes:
      - ./src:/src  # Mount source for hot reload
    environment:
      - DOTNET_USE_POLLING_FILE_WATCHER=1
    command: dotnet watch run --project /src/MyApp.Api/MyApp.Api.csproj
```

## Health Checks in Docker

```dockerfile
# In Dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1
```

```yaml
# In docker-compose.yml
services:
  api:
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 10s
```

## Container Orchestration Readiness

### Kubernetes-Ready Application

```csharp
// Program.cs - Configure for container environment
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP
    options.ListenAnyIP(8081, o => o.Protocols = HttpProtocols.Http2); // gRPC
});

// Graceful shutdown
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// Health endpoints for k8s probes
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
```

### Kubernetes Deployment Example

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: myapp-api
  template:
    metadata:
      labels:
        app: myapp-api
    spec:
      containers:
        - name: api
          image: myapp:latest
          ports:
            - containerPort: 8080
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
          resources:
            limits:
              memory: "512Mi"
              cpu: "500m"
            requests:
              memory: "256Mi"
              cpu: "250m"
```
