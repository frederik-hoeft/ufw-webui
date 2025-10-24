# UFW WebUI Architecture

## Overview

UFW WebUI is a secure web-based interface for managing UFW (Uncomplicated Firewall) rules on Linux systems. The architecture implements a **privilege separation model** where a web application runs in user space while a separate systemd service handles privileged firewall operations, communicating via named pipes.

## System Architecture

```
┌─────────────────┐  CRUD operations ┌───────────────────┐    UFW Commands  ┌───────────────────┐             ┌─────────┐
│   Web Browser   │ ◄─────────────►  │  Ufw.Web          │ ◄─────────────►  │ systemd service   │ ◄─────────► │   UFW   │
│   (User)        │      HTTPS       │ (Dockerized/user) │    IPC Channel   │ (Host/privileged) │             │ Daemon  │
└─────────────────┘                  └───────────────────┘                  └───────────────────┘             └─────────┘
                                             │                                     │
                                             ▼                                     ▼
                                      ┌──────────────┐                    ┌─────────────────┐
                                      │   SQLite     │                    │ System Firewall │
                                      │  Database    │                    │    Rules        │
                                      └──────────────┘                    └─────────────────┘
```

## Project Structure

### 🌐 Ufw.Web - Web Interface
**Purpose**: Dockerized ASP.NET Core web application providing the user interface
- **Technology**: .NET 9, ASP.NET Core, Razor Pages, Entity Framework Core
- **Database**: SQLite with ASP.NET Core Identity
- **Location**: `src/Ufw.Web/`
- **Key Features**:
  - User authentication and authorization
  - Firewall rule CRUD operations through web UI
  - Rule validation and input normalization pipeline
  - Integration with systemd service via named pipes

### ⚙️ Ufw.Systemd - Privileged Service
**Purpose**: AOT-compiled console service running as systemd daemon
- **Technology**: .NET 9 with AOT compilation (`PublishAot=true`)
- **Framework**: ConsoleAppFramework for source-generated command-line interface, Jab for source-generated dependency injection
- **Location**: `src/Ufw.Systemd/`
- **Key Features**:
  - Runs with elevated privileges to execute UFW commands
  - HTTP-like API controllers for processing firewall operation requests on behalf of the web application
  - Named pipe server for secure IPC
  - Configuration loading from `/etc/ufw-manager/settings.json`

### 🔗 Ufw.Pipes.Shared - IPC Foundation
**Purpose**: Shared models and protocols for named pipe communication
- **Technology**: .NET 9 Standard Library
- **Location**: `src/Ufw.Pipes.Shared/`
- **Key Components**:
  - Message serialization framework
  - Request/Response models for firewall operations
  - Transport layer abstractions
  - HTTP-style request methods (`GET`, `POST`, `PUT`, `DELETE`)

### 📡 Ufw.Pipes.Client - IPC Client Library
**Purpose**: Client-side abstractions for communicating with systemd service
- **Technology**: .NET 9 with DI integration
- **Location**: `src/Ufw.Pipes.Client/`
- **Key Interface**: `IUfwClient` - provides typed methods for sending requests

### 🔧 Ufw.Roslyn & Ufw.Roslyn.SourceGen - Code Generation
**Purpose**: Roslyn-based source generators for compile-time code generation
- **Technology**: Roslyn Analyzers (.NET Standard 2.0)
- **Location**: `src/Ufw.Roslyn/` and `src/Ufw.Roslyn.SourceGen/`
- **Key Generator**: `ApiEndpointBindingGenerator` - automatically generates API endpoint mappings from controller attributes

## Core Communication Pattern

### Named Pipe Architecture
The system uses **named pipes** for secure inter-process communication between the web application and systemd service:

```csharp
// Web Application Side (Ufw.Web)
public interface IUfwClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        RequestMethod method, 
        string route, 
        TRequest request, 
        CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;
}

// Systemd Service Side (Ufw.Systemd)
[Route("api/v1/rules")]
internal sealed class RulesController : ControllerBase
{
    [Get("list")]
    public async ValueTask<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken)
    
    [Delete]
    public async ValueTask<IResponseMessage> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
}
```

### Message Flow
1. **Web Request**: User interacts with Razor Pages
2. **Service Layer**: `UfwRuleService` processes business logic
3. **Pipe Client**: `IUfwClient` sends requests via named pipes
4. **Systemd Service**: API controllers handle requests and execute UFW commands
5. **Response**: Results flow back through the same pipeline

## Code Generation System

### Source Generator Architecture

The project uses Roslyn source generators to implement attribute-driven API endpoint mapping.

```csharp
// Attribute-driven controller registration
[ApiControllerRegistration<RulesController>]
[ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IMessage, IMessage>]
internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IMessage, IMessage>
{
    // Generated mappings automatically created at compile time
}
```

**Generated Output Example**:

```csharp
private static readonly ApiEndpointMapping<IMessage, IMessage>[] s_mappings = 
[
    UfwApiEndpointMappingFactory.Map<RuleListResponse>("GET", "/api/v1/rules/list", priority: 0,
        static async (serviceProvider, initializeAsync, cancellationToken) => 
        {
            var controller = await Activator.CreateControllerAsync<RulesController>(serviceProvider, initializeAsync, cancellationToken);
            return await controller.GetRulesAsync(cancellationToken);
        }),
];
```

## Development Patterns

### Friend Assembly Pattern
Uses `InternalsVisibleTo` for controlled access between projects:

```csharp
// src/Ufw.Pipes.Shared/_friends.cs
[assembly: InternalsVisibleTo("Ufw.Pipes.Client")]
```

### Global Usings Convention
Each project has `_globalusings.cs` for shared utilities:

```csharp
// Consistent across projects
global using static Ufw.Pipes.Shared.SyntacticSugar;
global using static Ufw.Pipes.Shared.Suppressions;
```

### Pipeline Normalization System
UFW rules use priority-based normalization in `Ufw.Web.Pipeline/`:

```csharp
// Interface pattern
public interface IRuleNormalizer
{
    int Priority { get; }
    void Normalize(UfwRule rule);
}

// Service orchestration
internal sealed class RuleNormalizationService(IEnumerable<IRuleNormalizer> normalizers)
{
    private readonly ImmutableArray<IRuleNormalizer> _normalizers = normalizers.CreatePipeline();
}
```

**Registered Normalizers**:
- `TrimWhitespaceNormalizer` - Removes leading/trailing whitespace
- `AnyValueNormalizer` - Normalizes "any" values in firewall rules

### Service Registration Pattern
All services follow interface-first design with scoped DI registration:

```csharp
// Program.cs pattern
builder.Services.AddScoped<IUfwRuleService, UfwRuleService>();
builder.Services.AddScoped<INetworkInterfaceService, NetworkInterfaceService>();
builder.Services.AddScoped<IRuleNormalizer, TrimWhitespaceNormalizer>();
```

## Build & Development Workflow

### Project Dependencies
```
Ufw.Web ──────────────────► Ufw.Pipes.Client ────► Ufw.Pipes.Shared
    │                                                       ▲
    └────► Ufw.Roslyn.SourceGen (Analyzer)                  │
                                                            │
Ufw.Systemd ─────────────────────────────────────────────┘
    │
    └────► Ufw.Roslyn.SourceGen (Analyzer)
```

### Key Commands

```bash
# Build entire solution
dotnet build src/Ufw.sln

# Run web interface (development)
dotnet run --project src/Ufw.Web

# Run systemd service
dotnet run --project src/Ufw.Systemd serve

# Run tests with parallelization
dotnet test src/Ufw.Web.Tests
```

### Build Configuration
- **Output Directory**: `src/artifacts/` (centralized)
- **Analysis Level**: `latest-all` with `EnforceCodeStyleInBuild=true`
- **Test Parallelization**: `[assembly: Parallelize]` for performance
- **AOT Compilation**: Enabled for `Ufw.Systemd` for faster startup and smaller footprint

## Security Model

### Privilege Separation
- **Web Application**: Runs as unprivileged user, handles UI and business logic
- **Systemd Service**: Runs with elevated privileges, limited to firewall operations only
- **Communication**: Secured through named pipes with controlled message protocols:
    - TODO: implement mutual TLS authentication for named pipe streams

### Configuration Security
- Service configuration: `/etc/ufw-manager/settings.json` (owned by root, `rw-r--r--`)
- Database: SQLite file with user authentication via ASP.NET Core Identity
- Secrets: ASP.NET Core User Secrets for development (`UserSecretsId` configured)

## Testing Strategy

### Test Organization
- **Location**: `src/Ufw.Web.Tests/`
- **Structure**: Mirrors main project structure (`Data/`, `Pipeline/`, `Services/`)
- **Parallelization**: Enabled via `[assembly: Parallelize]` attribute
- **Scope**: Focuses on business logic, pipeline components, and service layer

### Test Categories
- **Pipeline Tests**: Rule normalization and validation logic
- **Service Tests**: Business logic for firewall rule management
- **Data Tests**: Entity Framework contexts and data access patterns

## Integration Points

### External Dependencies
- **UFW**: System firewall managed through command-line interface
- **SQLite**: Local database for user data and rule persistence
- **Named Pipes**: OS-level IPC mechanism for secure communication
- **Systemd**: Service management for privileged daemon

### Internal Communication
- **Web → Systemd**: HTTP-style requests over named pipes
- **Controllers**: Shared attribute-based routing patterns between web and service
- **Models**: Shared data transfer objects in `Ufw.Pipes.Shared`

## Extension Points

### Adding New Firewall Operations
1. Define request/response models in `Ufw.Pipes.Shared/Model/`
2. Add controller methods with HTTP attributes in `Ufw.Systemd/Api/Controllers/`
3. Source generator automatically creates endpoint mappings
4. Implement client-side service methods in `Ufw.Web/Services/`

### Adding Rule Normalizers
1. Implement `IRuleNormalizer` with `Priority` property
2. Register as scoped service in `Program.cs`
3. Automatically included in normalization pipeline

This architecture provides a secure, maintainable foundation for firewall management while leveraging modern .NET features like source generators and AOT compilation for optimal performance and developer experience.