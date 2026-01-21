# Antigravity C# Backend Developer Kit

A CLI tool for initializing `.agent` configuration for C# Backend projects. This tool provides AI-assisted coding rules, skills, and workflows for the Antigravity IDE.

## Installation

Install the tool globally from NuGet:

```bash
dotnet tool install -g Antigravity.CSharp.BackendKit
```

Or update to the latest version:

```bash
dotnet tool update -g Antigravity.CSharp.BackendKit
```

## Usage

### Initialize .agent folder

Navigate to your C# project directory and run:

```bash
ag-csharp init
```

This creates a `.agent` folder with rules, skills, and workflows that your AI assistant will use.

### Commands

| Command | Description |
|---------|-------------|
| `ag-csharp init` | Initialize .agent folder in current directory |
| `ag-csharp init --force` | Overwrite existing .agent folder |
| `ag-csharp update` | Update existing .agent with latest content |
| `ag-csharp update --backup` | Update with backup of existing files |
| `ag-csharp list` | List all available rules, skills, and workflows |
| `ag-csharp list --rules` | List only rules |
| `ag-csharp list --skills` | List only skills |
| `ag-csharp list --workflows` | List only workflows |
| `ag-csharp validate` | Check .agent folder integrity |
| `ag-csharp version` | Display tool version |
| `ag-csharp --help` | Display help |

## Content Overview

### 📘 Rules (13 files)
Guidelines and standards for C# development:

- **01_csharp_standards.md** - Naming conventions, syntax, null safety
- **02_clean_architecture.md** - Onion layers, dependency rules
- **03_vertical_slice.md** - Feature folders, MediatR patterns
- **04_security_performance.md** - Input validation, async/await, caching
- **05_error_handling.md** - Exception patterns, Result type
- **06_logging.md** - Serilog, structured logging
- **07_api_design.md** - REST conventions, versioning
- **08_ef_core.md** - DbContext, queries, migrations
- **09_ddd_tactical.md** - Aggregates, Value Objects, Domain Events
- **10_ddd_strategic.md** - Bounded Contexts, Context Mapping
- **11_microservices.md** - Service boundaries, API contracts
- **12_distributed_systems.md** - Resilience, Polly, Circuit Breaker
- **13_containerization.md** - Docker best practices

### 🎯 Skills (17 files)
Code generation templates and prompts:

- **generate_entity.md** - Domain entities with business logic
- **generate_dto.md** - DTO/ViewModel creation
- **generate_repository.md** - Repository pattern
- **generate_service.md** - Business logic layer
- **generate_controller.md** - API endpoints
- **generate_unit_test.md** - xUnit/NUnit tests
- **generate_validation.md** - FluentValidation rules
- **generate_middleware.md** - Custom middleware
- **refactor_to_clean.md** - Code cleanup/refactoring
- **generate_aggregate.md** - DDD Aggregate Root
- **generate_value_object.md** - DDD Value Objects
- **generate_domain_event.md** - Domain Events
- **generate_dockerfile.md** - Multi-stage Dockerfile
- **generate_docker_compose.md** - Docker Compose setup
- **generate_health_check.md** - Health/readiness endpoints
- **generate_grpc_service.md** - gRPC services
- **generate_message_handler.md** - Message queue handlers

### 🔄 Workflows (8 files)
Step-by-step processes for common tasks:

- **feature_implementation.md** - End-to-end feature development
- **code_review.md** - Pre-merge review checklist
- **debugging.md** - Issue resolution process
- **database_migration.md** - Safe schema evolution
- **testing.md** - Test creation workflow
- **microservice_creation.md** - New service setup
- **ddd_modeling.md** - Domain modeling process
- **containerization.md** - Docker setup workflow

## Folder Structure

After running `ag-csharp init`, you'll have:

```
your-project/
├── .agent/
│   ├── rules/
│   │   ├── 01_csharp_standards.md
│   │   ├── 02_clean_architecture.md
│   │   └── ... (13 files)
│   ├── skills/
│   │   ├── generate_entity.md
│   │   ├── generate_dto.md
│   │   └── ... (17 files)
│   └── workflows/
│       ├── feature_implementation.md
│       ├── code_review.md
│       └── ... (8 files)
└── ... (your project files)
```

## Requirements

- .NET 8.0 or .NET 9.0

## Development

### Build from Source

```bash
git clone https://github.com/antigravity/csharp-backend-kit.git
cd csharp-backend-kit
dotnet build
dotnet pack
```

### Install Local Build

```bash
dotnet tool install -g --add-source ./src/AgCsharp/nupkg Antigravity.CSharp.BackendKit
```

### Uninstall

```bash
dotnet tool uninstall -g Antigravity.CSharp.BackendKit
```

## License

MIT License - see LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
