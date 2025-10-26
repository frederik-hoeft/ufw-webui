# UFW WebUI - AI Coding Assistant Instructions

## Project Overview
This is a UFW (Uncomplicated Firewall) web management interface with a multi-project .NET 9 architecture implementing **privilege separation**: a web application (user space) communicates with a systemd service (privileged) via named pipes for secure firewall management.

## Code Style Guidelines
See `/code-style.md` for detailed C# coding conventions to be used across all projects.

## Architecture Components

### Core Projects Structure
- **`Ufw.Web`**: ASP.NET Core web interface with SQLite, Identity, and Razor Pages
- **`Ufw.Systemd`**: AOT-compiled multi-worker service with HTTP-style API controllers over named pipes
- **`Ufw.Ipc.Shared`**: Named pipe communication layer with serialization and transport
- **`Ufw.Ipc.Client`**: Client-side pipe communication abstractions  
- **`Ufw.Roslyn`** & **`Ufw.Roslyn.SourceGen`**: Source generation for automatic API endpoint mapping

### Key Communication Pattern
**Named Pipe IPC with HTTP-style semantics**:
- Web UI → `IUfwClient` → Named Pipes → Systemd Service → UFW commands
- Controllers use `[Route]`, `[Get]`, `[Post]`, etc. attributes for API-like patterns
- Source generator automatically creates endpoint mappings from controller attributes
- Multi-worker concurrent processing with middleware pipeline

## Critical Development Patterns

### Source-Generated API Endpoints
The systemd service uses **attribute-driven endpoint generation**:
```csharp
// 1. Define controller with HTTP attributes
[Route("api/v1/rules")]
internal sealed class RulesController : ControllerBase
{
    [Get("list")]
    public async ValueTask<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken)
    
    [Delete]  
    public async ValueTask<IResponseMessage> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
}

// 2. Register in UfwApiEndpointMap.cs - triggers source generation
[ApiControllerRegistration<RulesController>]
[ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IMessage, IMessage>]
internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IMessage, IMessage>
```

### Friend Assembly Pattern
Uses `InternalsVisibleTo` in `_friends.cs` files for controlled internal access between projects:
```csharp
[assembly: InternalsVisibleTo("Ufw.Ipc.Client")]
```

### Global Usings Pattern  
Each project has `_globalusings.cs` importing shared utilities:
```csharp
global using static Ufw.Ipc.Shared.SyntacticSugar;
global using static Ufw.Ipc.Shared.Suppressions;
```

### Pipeline Normalization System
UFW rules use priority-based normalization in `Ufw.Web.Pipeline`:
- Implement `IRuleNormalizer` with `Priority` property
- Register all normalizers as scoped services
- `RuleNormalizationService` orchestrates execution by priority

### Service Registration Pattern
Services follow interface-first design with scoped DI registration:
```csharp
builder.Services.AddScoped<IUfwRuleService, UfwRuleService>();
builder.Services.AddScoped<IRuleNormalizer, TrimWhitespaceNormalizer>();
```

## Development Workflows

### Build & Test
- Solution root: `src/Ufw.sln`
- Output: `src/artifacts/` directory
- Tests use `[assembly: Parallelize]` for performance
- Strict analysis: `AnalysisLevel=latest-all` with `EnforceCodeStyleInBuild=true`

### Key Commands
```bash
# Build solution
dotnet build src/Ufw.sln

# Run web interface (development)
dotnet run --project src/Ufw.Web

# Run systemd service (development)  
dotnet run --project src/Ufw.Systemd serve

# Run with custom config
dotnet run --project src/Ufw.Systemd serve --config /path/to/settings.json

# Test with parallelization
dotnet test src/Ufw.Web.Tests

# View generated source files (debugging)
find . -name "*.g.cs" -type f
```

### Source Generation Integration
Projects reference `Ufw.Roslyn.SourceGen` as analyzer:
```xml
<ProjectReference Include="..\Ufw.Roslyn.SourceGen\Ufw.Roslyn.SourceGen.csproj" 
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## Adding New Functionality

### Adding Domain Endpoints (Complete Workflow)
1. **Define Models** in `Ufw.Ipc.Shared/Model/Requests/` and `/Responses/`:
   ```csharp
   public sealed record CreateRuleRequest(string From, string To, string Action) : IMessagePayload;
   public sealed record CreateRuleResponse(bool Success, string? ErrorMessage) : IResponseMessage;
   ```

2. **Create Controller Method** with HTTP attributes:
   ```csharp
   [Post("create")]
   public async ValueTask<CreateRuleResponse> CreateRuleAsync(CreateRuleRequest request, CancellationToken cancellationToken)
   ```

3. **Register Controller** in `UfwApiEndpointMap.cs` (triggers source generation)
4. **Add Client Method** in `Ufw.Web/Services/` using `IUfwClient`
5. **Source generator automatically creates endpoint mappings** - no manual registration needed

### Adding Rule Normalizers
1. Implement `IRuleNormalizer` with `Priority` property
2. Register as scoped service in `Program.cs`
3. Automatically included in normalization pipeline

## File Organization Conventions
- `Models/`: Data models and DTOs
- `Services/`: Business logic with interface/implementation pairs  
- `Pipeline/`: Normalization and processing logic
- `Transport/`: Named pipe communication layer
- `Configuration/`: Service configuration and DI setup
- `Api/Controllers/`: HTTP-style controllers for systemd service
- `Api/Middleware/`: Request processing pipeline components

## Key Integration Points
- **Configuration**: Service loads from `/etc/ufw-manager/settings.json` (production) or `appsettings.json` (development)
- **Security**: Named pipes with planned mutual TLS authentication
- **Dependency Injection**: Jab source-generated DI in systemd service, built-in DI in web app
- **Routing**: Source-generated endpoint mappings with HTTP-style semantics over named pipes

When adding new features, maintain the interface-first service pattern, use source generation for API endpoints, and follow the established project reference hierarchy.