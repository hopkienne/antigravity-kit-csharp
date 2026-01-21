# Skill: Generate Docker Compose

## When to Use
User requests to create Docker Compose configuration for local development or multi-container setup.

## Template

```yaml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "{host_port}:{container_port}"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - db

  db:
    image: {database_image}
    ports:
      - "{db_port}:{db_port}"
    volumes:
      - {db_volume}:/var/lib/{db}
    environment:
      - {DB_ENV_VARS}

volumes:
  {db_volume}:
```

## Example Output

### Full Development Environment

```yaml
# docker-compose.yml
version: '3.8'

services:
  # ===========================================
  # API Service
  # ===========================================
  api:
    build:
      context: .
      dockerfile: src/MyApp.Api/Dockerfile
    container_name: myapp-api
    ports:
      - "8080:8080"
      - "8081:8081"  # HTTPS (if configured)
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__Default=Server=db;Database=MyApp;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true
      - Redis__Connection=redis:6379
      - Seq__ServerUrl=http://seq:80
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_started
    networks:
      - myapp-network
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  # ===========================================
  # SQL Server Database
  # ===========================================
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: myapp-db
    ports:
      - "1433:1433"
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd
      - MSSQL_PID=Developer
    volumes:
      - sqlserver-data:/var/opt/mssql
    networks:
      - myapp-network
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$$MSSQL_SA_PASSWORD" -Q "SELECT 1" -b -o /dev/null
      interval: 10s
      timeout: 3s
      retries: 10
      start_period: 10s

  # ===========================================
  # Redis Cache
  # ===========================================
  redis:
    image: redis:7-alpine
    container_name: myapp-redis
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    networks:
      - myapp-network
    command: redis-server --appendonly yes

  # ===========================================
  # Seq Logging Server
  # ===========================================
  seq:
    image: datalust/seq:latest
    container_name: myapp-seq
    ports:
      - "5341:80"
    environment:
      - ACCEPT_EULA=Y
    volumes:
      - seq-data:/data
    networks:
      - myapp-network

  # ===========================================
  # RabbitMQ Message Broker (Optional)
  # ===========================================
  rabbitmq:
    image: rabbitmq:3-management-alpine
    container_name: myapp-rabbitmq
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    environment:
      - RABBITMQ_DEFAULT_USER=guest
      - RABBITMQ_DEFAULT_PASS=guest
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    networks:
      - myapp-network

# ===========================================
# Volumes
# ===========================================
volumes:
  sqlserver-data:
    driver: local
  redis-data:
    driver: local
  seq-data:
    driver: local
  rabbitmq-data:
    driver: local

# ===========================================
# Networks
# ===========================================
networks:
  myapp-network:
    driver: bridge
```

### Development Override

```yaml
# docker-compose.override.yml
# Automatically merged with docker-compose.yml in development

version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: src/MyApp.Api/Dockerfile
      target: development  # Use development stage
    volumes:
      - ./src:/src:cached  # Mount source for hot reload
      - ~/.nuget/packages:/root/.nuget/packages:ro  # Cache NuGet packages
    environment:
      - DOTNET_USE_POLLING_FILE_WATCHER=1
      - DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1
    command: dotnet watch run --project /src/MyApp.Api/MyApp.Api.csproj --urls http://+:8080
```

### Production Override

```yaml
# docker-compose.prod.yml
# Use: docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

version: '3.8'

services:
  api:
    image: myregistry.azurecr.io/myapp:${TAG:-latest}
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

### PostgreSQL Alternative

```yaml
# PostgreSQL instead of SQL Server
services:
  db:
    image: postgres:16-alpine
    container_name: myapp-postgres
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
      - POSTGRES_DB=myapp
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - myapp-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Connection string for API:
  # ConnectionStrings__Default=Host=db;Database=myapp;Username=postgres;Password=postgres
```

### Microservices Setup

```yaml
# docker-compose.microservices.yml
version: '3.8'

services:
  # API Gateway
  gateway:
    build:
      context: ./src/Gateway
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - customer-service
      - order-service
      - product-service
    networks:
      - myapp-network

  # Customer Service
  customer-service:
    build:
      context: ./src/CustomerService
      dockerfile: Dockerfile
    environment:
      - ConnectionStrings__Default=Server=customer-db;Database=Customers;...
    depends_on:
      - customer-db
    networks:
      - myapp-network

  customer-db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=YourStrong@Passw0rd
    volumes:
      - customer-db-data:/var/opt/mssql
    networks:
      - myapp-network

  # Order Service
  order-service:
    build:
      context: ./src/OrderService
      dockerfile: Dockerfile
    environment:
      - ConnectionStrings__Default=Server=order-db;Database=Orders;...
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - order-db
      - rabbitmq
    networks:
      - myapp-network

  order-db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_DB=orders
      - POSTGRES_PASSWORD=postgres
    volumes:
      - order-db-data:/var/lib/postgresql/data
    networks:
      - myapp-network

  # Product Service
  product-service:
    build:
      context: ./src/ProductService
      dockerfile: Dockerfile
    environment:
      - MongoDB__ConnectionString=mongodb://mongo:27017
    depends_on:
      - mongo
    networks:
      - myapp-network

  mongo:
    image: mongo:7
    volumes:
      - mongo-data:/data/db
    networks:
      - myapp-network

  # Shared Infrastructure
  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports:
      - "15672:15672"
    networks:
      - myapp-network

volumes:
  customer-db-data:
  order-db-data:
  mongo-data:

networks:
  myapp-network:
    driver: bridge
```

### Common Commands

```bash
# Start all services
docker-compose up -d

# Start with build
docker-compose up -d --build

# Start specific service
docker-compose up -d api db

# View logs
docker-compose logs -f api

# Stop all services
docker-compose down

# Stop and remove volumes
docker-compose down -v

# Use override file
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Scale service
docker-compose up -d --scale api=3

# Execute command in container
docker-compose exec api dotnet ef database update
```

## Guidelines

1. **Version 3.8** - Modern features, good compatibility
2. **Named volumes** - Persist data across restarts
3. **Health checks** - Enable dependency ordering
4. **Networks** - Isolate services
5. **Override files** - Separate dev/prod configs
6. **Environment variables** - Externalize configuration
7. **Resource limits** - Prevent runaway containers
