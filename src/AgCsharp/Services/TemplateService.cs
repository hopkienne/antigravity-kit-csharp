using System.Reflection;

namespace AgCsharp.Services;

public class TemplateService
{
    private readonly Assembly _assembly;
    private const string ResourcePrefix = "AgCsharp.Templates.";

    public TemplateService()
    {
        _assembly = Assembly.GetExecutingAssembly();
    }

    public async Task<ExtractionResult> ExtractTemplatesAsync(string targetPath)
    {
        try
        {
            var resourceNames = _assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(ResourcePrefix))
                .ToList();

            if (resourceNames.Count == 0)
            {
                return new ExtractionResult
                {
                    Success = false,
                    ErrorMessage = "No embedded templates found in assembly."
                };
            }

            int rulesCount = 0, skillsCount = 0, workflowsCount = 0;

            foreach (var resourceName in resourceNames)
            {
                // Convert resource name to file path
                // AgCsharp.Templates..agent.rules.01_csharp_standards.md
                // -> .agent/rules/01_csharp_standards.md
                
                // Remove the prefix to get: .agent.rules.01_csharp_standards.md
                var afterPrefix = resourceName.Substring(ResourcePrefix.Length);
                
                // Handle the .agent folder (starts with a dot, causing double dot after prefix)
                // The embedded resource shows as: AgCsharp.Templates..agent.rules...
                // After removing prefix: .agent.rules.01_csharp_standards.md
                
                // Split by dots and reconstruct
                var parts = afterPrefix.Split('.');
                
                // Expected structure: ["", "agent", "rules", "01_csharp_standards", "md"]
                // or: ["", "agent", "skills", "generate_entity", "md"]
                if (parts.Length < 4)
                    continue;
                
                // parts[0] = "" (from leading dot in .agent)
                // parts[1] = "agent"
                // parts[2] = "rules", "skills", or "workflows"
                // parts[3...n-1] = filename parts
                // parts[n] = "md" (extension)
                var category = parts[2]; // "rules", "skills", or "workflows"
                
                // Join remaining parts for filename (handle underscores in filenames)
                var fileNameParts = parts.Skip(3).ToArray();
                var fileName = string.Join(".", fileNameParts);
                
                // Build the relative path - targetPath already points to .agent folder
                var relativePath = Path.Combine(category, fileName);
                var fullPath = Path.Combine(targetPath, relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Extract resource content
                using var stream = _assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    await File.WriteAllTextAsync(fullPath, content);

                    // Count by category
                    if (fullPath.Contains(Path.Combine("rules", "")))
                        rulesCount++;
                    else if (fullPath.Contains(Path.Combine("skills", "")))
                        skillsCount++;
                    else if (fullPath.Contains(Path.Combine("workflows", "")))
                        workflowsCount++;
                }
            }

            return new ExtractionResult
            {
                Success = true,
                RulesCount = rulesCount,
                SkillsCount = skillsCount,
                WorkflowsCount = workflowsCount
            };
        }
        catch (Exception ex)
        {
            return new ExtractionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public ContentCatalog GetContentCatalog()
    {
        return new ContentCatalog
        {
            Rules = new List<ContentItem>
            {
                new("01_csharp_standards.md", "C# naming conventions, syntax, null safety"),
                new("02_clean_architecture.md", "Onion layers, dependency rules"),
                new("03_vertical_slice.md", "Feature folders, MediatR patterns"),
                new("04_security_performance.md", "Input validation, async/await, caching"),
                new("05_error_handling.md", "Exception patterns, Result type"),
                new("06_logging.md", "Serilog, structured logging"),
                new("07_api_design.md", "REST conventions, versioning"),
                new("08_ef_core.md", "DbContext, queries, migrations"),
                new("09_ddd_tactical.md", "Aggregates, Value Objects, Domain Events"),
                new("10_ddd_strategic.md", "Bounded Contexts, Context Mapping"),
                new("11_microservices.md", "Service boundaries, API contracts"),
                new("12_distributed_systems.md", "Resilience, Polly, Circuit Breaker"),
                new("13_containerization.md", "Docker best practices, multi-stage builds")
            },
            Skills = new List<ContentItem>
            {
                new("generate_entity.md", "Domain entity with business logic"),
                new("generate_dto.md", "DTO/ViewModel creation"),
                new("generate_repository.md", "Repository pattern implementation"),
                new("generate_service.md", "Business logic layer"),
                new("generate_controller.md", "API endpoints"),
                new("generate_unit_test.md", "Unit tests (xUnit/NUnit)"),
                new("generate_validation.md", "FluentValidation rules"),
                new("generate_middleware.md", "Custom middleware"),
                new("refactor_to_clean.md", "Code cleanup/refactoring"),
                new("generate_aggregate.md", "DDD Aggregate Root"),
                new("generate_value_object.md", "DDD Value Objects"),
                new("generate_domain_event.md", "DDD Domain Events"),
                new("generate_dockerfile.md", "Multi-stage Dockerfile"),
                new("generate_docker_compose.md", "Docker Compose setup"),
                new("generate_health_check.md", "Health/readiness endpoints"),
                new("generate_grpc_service.md", "gRPC service implementation"),
                new("generate_message_handler.md", "Message queue handlers")
            },
            Workflows = new List<ContentItem>
            {
                new("feature_implementation.md", "Entity -> Repo -> Service -> API"),
                new("code_review.md", "Pre-merge review checklist"),
                new("debugging.md", "Issue resolution process"),
                new("database_migration.md", "Safe schema evolution"),
                new("testing.md", "Test creation workflow"),
                new("microservice_creation.md", "New microservice setup"),
                new("ddd_modeling.md", "Domain modeling process"),
                new("containerization.md", "Docker setup workflow")
            }
        };
    }
}

public class ExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RulesCount { get; set; }
    public int SkillsCount { get; set; }
    public int WorkflowsCount { get; set; }
    public int TotalCount => RulesCount + SkillsCount + WorkflowsCount;
}

public class ContentCatalog
{
    public List<ContentItem> Rules { get; set; } = [];
    public List<ContentItem> Skills { get; set; } = [];
    public List<ContentItem> Workflows { get; set; } = [];
}

public record ContentItem(string Name, string Description);
