# Skill: Generate Dockerfile

## When to Use
User requests to create a Dockerfile for containerizing a .NET application.

## Template

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:{version} AS build
WORKDIR /src

# Copy and restore
COPY ["{project}.csproj", "{project}/"]
RUN dotnet restore "{project}/{project}.csproj"

# Build
COPY . .
WORKDIR "/src/{project}"
RUN dotnet build "{project}.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "{project}.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:{version} AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "{project}.dll"]
```

## Example Output

### Standard ASP.NET Core API

```dockerfile
# ============================================
# Multi-stage Dockerfile for ASP.NET Core API
# ============================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for better caching
COPY ["MyApp.sln", "./"]
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
COPY ["src/MyApp.Application/MyApp.Application.csproj", "src/MyApp.Application/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["src/MyApp.Infrastructure/MyApp.Infrastructure.csproj", "src/MyApp.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/src/MyApp.Api"
RUN dotnet build "MyApp.Api.csproj" -c Release -o /app/build --no-restore

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "MyApp.Api.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup appuser \
    && chown -R appuser:appgroup /app

USER appuser

# Copy published output
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

### Minimal/Chiseled Image (Smallest Size)

```dockerfile
# ============================================
# Minimal Dockerfile with Chiseled Image
# ============================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

COPY . .
WORKDIR "/src/src/MyApp.Api"

# Publish as self-contained with trimming
RUN dotnet publish "MyApp.Api.csproj" -c Release -o /app/publish \
    --self-contained true \
    -r linux-x64 \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true \
    /p:EnableCompressionInSingleFile=true

# Chiseled image - ultra-minimal, no shell, non-root by default
FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:8.0-jammy-chiseled AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["./MyApp.Api"]
```

### With Build Arguments and Secrets

```dockerfile
# ============================================
# Dockerfile with Build Args and Secrets
# ============================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Build arguments
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0

WORKDIR /src

# Use secret for NuGet config (BuildKit required)
RUN --mount=type=secret,id=nuget_config,target=/root/.nuget/NuGet/NuGet.Config \
    echo "NuGet config mounted"

COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

COPY . .
WORKDIR "/src/src/MyApp.Api"

RUN dotnet build "MyApp.Api.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/build \
    /p:Version=${VERSION}

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MyApp.Api.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Labels
LABEL org.opencontainers.image.title="MyApp API"
LABEL org.opencontainers.image.version="${VERSION}"
LABEL org.opencontainers.image.description="MyApp API Service"

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "MyApp.Api.dll"]
```

### Development Dockerfile (with Hot Reload)

```dockerfile
# ============================================
# Development Dockerfile with Hot Reload
# ============================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development

# Install tools for debugging
RUN dotnet tool install --global dotnet-ef
RUN dotnet tool install --global dotnet-watch
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /src

# Copy project files
COPY ["src/MyApp.Api/MyApp.Api.csproj", "src/MyApp.Api/"]
COPY ["src/MyApp.Application/MyApp.Application.csproj", "src/MyApp.Application/"]
COPY ["src/MyApp.Domain/MyApp.Domain.csproj", "src/MyApp.Domain/"]
COPY ["src/MyApp.Infrastructure/MyApp.Infrastructure.csproj", "src/MyApp.Infrastructure/"]
RUN dotnet restore "src/MyApp.Api/MyApp.Api.csproj"

# Copy source (volume mount will override this)
COPY . .

WORKDIR "/src/src/MyApp.Api"

# Enable hot reload
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1

EXPOSE 8080
EXPOSE 8081

# Use dotnet watch for hot reload
ENTRYPOINT ["dotnet", "watch", "run", "--urls", "http://+:8080"]
```

### .dockerignore

```dockerignore
# Git
.git
.gitignore
.gitattributes

# Build outputs
**/bin
**/obj
**/out
**/publish

# IDE and editor files
.vs
.vscode
.idea
*.user
*.suo
*.sln.docstates

# Docker files (don't copy into build context)
**/Dockerfile*
**/docker-compose*
**/.dockerignore

# Test files (optional - exclude if not needed in image)
**/*Tests
**/*Tests.csproj
**/TestResults

# Documentation
*.md
LICENSE
docs/

# Local configuration
**/appsettings.Development.json
**/appsettings.Local.json
**/.env
**/.env.*

# Logs
logs/
*.log

# Misc
Thumbs.db
.DS_Store
```

### Build Commands

```bash
# Build image
docker build -t myapp:latest .

# Build with build args
docker build \
    --build-arg BUILD_CONFIGURATION=Release \
    --build-arg VERSION=1.2.3 \
    -t myapp:1.2.3 .

# Build with secrets (BuildKit)
DOCKER_BUILDKIT=1 docker build \
    --secret id=nuget_config,src=./NuGet.Config \
    -t myapp:latest .

# Build for specific platform
docker build --platform linux/amd64 -t myapp:latest .

# Run container
docker run -d \
    -p 8080:8080 \
    -e ConnectionStrings__Default="Server=..." \
    --name myapp \
    myapp:latest
```

## Guidelines

1. **Multi-stage builds** - Separate build and runtime
2. **Layer caching** - Copy csproj first, then source
3. **Non-root user** - Security best practice
4. **Health checks** - Enable orchestrator health monitoring
5. **Minimal base images** - Use alpine or chiseled when possible
6. **.dockerignore** - Reduce build context size
7. **Environment variables** - Configure via ENV, not hardcoded
