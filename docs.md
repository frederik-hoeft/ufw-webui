# UFW WebUI Architecture


<!-- @import "[TOC]" {cmd="toc" depthFrom=2 depthTo=6 orderedList=false} -->

<!-- code_chunk_output -->

- [Overview](#overview)
- [System Architecture](#system-architecture)
- [Project Structure](#project-structure)
  - [🌐 Ufw.Web - Web Interface](#-ufwweb---web-interface)
  - [⚙️ Ufw.Systemd - Privileged Service Architecture](#️-ufwsystemd---privileged-service-architecture)
    - [Service Startup & Configuration](#service-startup--configuration)
    - [Multi-Worker Network Architecture](#multi-worker-network-architecture)
    - [Request Processing Pipeline](#request-processing-pipeline)
    - [API Controller System](#api-controller-system)
    - [Endpoint Routing & Mapping](#endpoint-routing--mapping)
    - [Adding New Domain Endpoints](#adding-new-domain-endpoints)
    - [Dependency Injection Architecture](#dependency-injection-architecture)
    - [Key Features](#key-features)
  - [🔗 Ufw.Pipes.Shared - IPC Foundation](#-ufwpipesshared---ipc-foundation)
  - [📡 Ufw.Pipes.Client - IPC Client Library](#-ufwpipesclient---ipc-client-library)
  - [🔧 Ufw.Roslyn & Ufw.Roslyn.SourceGen - Code Generation](#-ufwroslyn--ufwroslynsourcegen---code-generation)
- [Core Communication Pattern](#core-communication-pattern)
  - [Named Pipe Architecture](#named-pipe-architecture)
  - [Message Flow](#message-flow)
- [Code Generation System](#code-generation-system)
  - [Source Generator Architecture](#source-generator-architecture)
- [Development Patterns](#development-patterns)
  - [Friend Assembly Pattern](#friend-assembly-pattern)
  - [Global Usings Convention](#global-usings-convention)
  - [Pipeline Normalization System](#pipeline-normalization-system)
  - [Service Registration Pattern](#service-registration-pattern)
- [Build & Development Workflow](#build--development-workflow)
  - [Project Dependencies](#project-dependencies)
  - [Key Commands](#key-commands)
  - [Development & Debugging Workflow](#development--debugging-workflow)
    - [Systemd Service Development](#systemd-service-development)
    - [Configuration Management](#configuration-management)
    - [Source Generator Debugging](#source-generator-debugging)
  - [Build Configuration](#build-configuration)
- [Security Model](#security-model)
  - [Privilege Separation](#privilege-separation)
  - [Configuration Security](#configuration-security)
- [Testing Strategy](#testing-strategy)
  - [Test Organization](#test-organization)
  - [Test Categories](#test-categories)
- [Integration Points](#integration-points)
  - [External Dependencies](#external-dependencies)
  - [Internal Communication](#internal-communication)
- [Extension Points](#extension-points)
  - [Adding New Firewall Operations](#adding-new-firewall-operations)
    - [1. Define Request/Response Models](#1-define-requestresponse-models)
    - [2. Implement Controller Method](#2-implement-controller-method)
    - [3. Register Controller (if new)](#3-register-controller-if-new)
    - [4. Source Generator Automation](#4-source-generator-automation)
    - [5. Implement Web Service Methods](#5-implement-web-service-methods)
    - [6. Update Web UI](#6-update-web-ui)
  - [Adding Rule Normalizers](#adding-rule-normalizers)

<!-- /code_chunk_output -->



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

### ⚙️ Ufw.Systemd - Privileged Service Architecture

**Purpose**: AOT-compiled console service running as systemd daemon with sophisticated request processing pipeline

- **Technology**: .NET 9 with AOT compilation (`PublishAot=true`)
- **Framework**: ConsoleAppFramework for source-generated command-line interface, Jab for source-generated dependency injection
- **Location**: `src/Ufw.Systemd/`

#### Service Startup & Configuration
```csharp
// Entry point: Commands.cs
[Command("serve")]
public async Task ServeAsync(string config = "/etc/ufw-manager/settings.json", CancellationToken cancellationToken = default)
{
    await using DefaultServiceProvider serviceProvider = new();
    IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
    bool success = await configuration.TryReloadAsync(config, cancellationToken);
    INetworkApplication networkApp = serviceProvider.GetService<INetworkApplication>();
    await networkApp.RunAsync(cancellationToken);
}
```

#### Multi-Worker Network Architecture
The service implements a **multi-worker concurrent processing model**:

```csharp
// NetworkApplication.cs - Spawns multiple workers
public async Task RunAsync(CancellationToken cancellationToken)
{
    List<Task> workerTasks = new(_maxWorkers);
    for (int i = 0; i < _maxWorkers; i++)
    {
        INetworkApplicationWorker worker = serviceProvider.GetRequiredService<INetworkApplicationWorker>();
        Task workerTask = worker.ServeAsync(this, cancellationToken);
        workerTasks.Add(workerTask);
    }
    await Task.WhenAll(workerTasks);
}
```

Each worker handles the complete request lifecycle:
1. **Transport Layer**: Accept named pipe connections
2. **Security Layer**: Establish mutual TLS encryption  
3. **Serialization**: Deserialize request messages
4. **Pipeline Processing**: Route through middleware chain
5. **Response**: Serialize and return response

#### Request Processing Pipeline

The service uses a **middleware pipeline pattern** for request processing:

```csharp
// Request flow: NetworkApplicationWorker.cs
await using ITransportLayerConnection connection = await transportLayerService.ServeAsync(cancellationToken);
await using Stream networkStream = connection.GetStream(readTimeout: timeout, writeTimeout: timeout);
await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, cancellationToken);
await using IMessage requestEnvelope = await messageSerializer.ReadAsync(secureStream, cancellationToken);
await using IMessage responseEnvelope = await requestResponsePipeline.ProcessMessageAsync(requestEnvelope, cancellationToken);
await messageSerializer.WriteAsync(secureStream, responseEnvelope, cancellationToken);
```

**Middleware Chain** (executed in priority order):
1. **Request Validation Middleware** - Validates message structure and required fields
2. **Endpoint Invocation Middleware** - Routes to appropriate controller and executes handler

#### API Controller System

Controllers follow **ASP.NET Core-style patterns** but execute over named pipes:

```csharp
// Example: RulesController.cs
[Route("api/v1/rules")]
internal sealed class RulesController(IConfiguration configuration) : ControllerBase
{
    [Get("list")]
    public async ValueTask<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken)
    
    [Delete]
    public async ValueTask<IResponseMessage> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
}
```

#### Endpoint Routing & Mapping

The service uses **source-generated endpoint mappings** for routing:

```csharp
// UfwApiEndpointMap.cs - Attributes trigger source generation
[ApiControllerRegistration<RulesController>]
[ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IMessage, IMessage>]
internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IMessage, IMessage>
```

**Generated Mapping Structure**:
```csharp
// Auto-generated endpoint mappings
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

#### Adding New Domain Endpoints

**Step-by-step process for adding new firewall operations**:

1. **Define Models** in `Ufw.Pipes.Shared/Model/`:
   ```csharp
   // Request model
   public sealed record CreateRuleRequest(string From, string To, string Action) : IMessagePayload;
   
   // Response model  
   public sealed record CreateRuleResponse(bool Success, string? ErrorMessage) : IResponseMessage;
   ```

2. **Create Controller** in `Ufw.Systemd/Api/Controllers/`:
   ```csharp
   [Route("api/v1/rules")]
   internal sealed class RulesController : ControllerBase
   {
       [Post("create")]
       public async ValueTask<CreateRuleResponse> CreateRuleAsync(CreateRuleRequest request, CancellationToken cancellationToken)
       {
           // Implementation here
       }
   }
   ```

3. **Register Controller** in `UfwApiEndpointMap.cs`:
   ```csharp
   [ApiControllerRegistration<RulesController>]  // ← Add this line
   [ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IMessage, IMessage>]
   internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IMessage, IMessage>
   ```

4. **Source Generator** automatically creates endpoint mappings at compile time

5. **Add Client Methods** in `Ufw.Web/Services/`:
   ```csharp
   public async Task<CreateRuleResponse> CreateRuleAsync(CreateRuleRequest request)
   {
       return await _ufwClient.SendAsync<CreateRuleRequest, CreateRuleResponse>(
           RequestMethod.Post, "/api/v1/rules/create", request);
   }
   ```

#### Dependency Injection Architecture

The systemd service uses **Jab** for source-generated dependency injection with modular design:

```csharp
// DefaultServiceProvider.cs - Central DI container
[ServiceProvider]
[Import<IConfigurationModule>]     // Configuration services
[Import<INetworkModule>]           // Network application services  
[Import<IPipeTransportModule>]     // Named pipe transport
[Import<IApiModule>]               // API controllers and middleware
[Singleton<ILogger, ConsoleLogger>]
[Singleton<ICertificateLoader, PemCertificateLoader>]
internal sealed partial class DefaultServiceProvider;
```

**Module Structure**:
- **IConfigurationModule**: Settings loading and validation
- **INetworkModule**: Multi-worker network application  
- **IPipeTransportModule**: Named pipe server and security
- **IApiModule**: Controllers, middleware, and endpoint mapping

**Benefits**:
- **AOT-Compatible**: No runtime reflection, fully AOT-compiled
- **Performance**: Zero-cost abstractions with compile-time DI resolution
- **Modularity**: Clean separation of concerns across service layers

#### Key Features
- **Concurrent Processing**: Multiple workers handle simultaneous requests
- **Type Safety**: Strongly-typed request/response models with compile-time validation
- **Security**: Mutual TLS authentication over named pipes (planned implementation)
- **Configuration**: Runtime configuration loading from `/etc/ufw-manager/settings.json`
- **Error Handling**: Structured error responses with optional debug information
- **Source Generation**: Zero-runtime-cost endpoint mapping via Roslyn generators

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

# Run systemd service (development)
dotnet run --project src/Ufw.Systemd serve

# Run systemd service with custom config
dotnet run --project src/Ufw.Systemd serve --config /path/to/settings.json

# Run tests with parallelization
dotnet test src/Ufw.Web.Tests

# Publish systemd service for deployment (AOT)
dotnet publish src/Ufw.Systemd -c Release
```

### Development & Debugging Workflow

#### Systemd Service Development
```bash
# 1. Build and test locally
dotnet build src/Ufw.Systemd

# 2. Run with development settings
dotnet run --project src/Ufw.Systemd serve --config appsettings.json

# 3. Monitor request/response flow (when DebugMode: true)
# Service logs detailed request processing information

# 4. Test API endpoints directly (development)
# Use integration tests or pipe communication test tools
```

#### Configuration Management
```json
// /etc/ufw-manager/settings.json (production)
// src/Ufw.Systemd/appsettings.json (development)
{
  "DebugMode": true,
  "WriteToConsole": true,
  "UfwPath": "/usr/sbin/ufw",
  "Network": {
    "MaxConnections": 10,
    "RequestTimeout": "00:00:30"
  },
  "Pipe": {
    // Named pipe configuration
  }
}
```

#### Source Generator Debugging
```bash
# View generated source files
find . -name "*.g.cs" -type f

# Force regeneration during development
dotnet clean && dotnet build

# Debug source generator issues
dotnet build -v diagnostic
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

**Complete workflow for adding new domain endpoints**:

#### 1. Define Request/Response Models
Add strongly-typed models in `Ufw.Pipes.Shared/Model/Requests/` and `Ufw.Pipes.Shared/Model/Responses/`:

```csharp
// Ufw.Pipes.Shared/Model/Requests/Domain/CreateRuleRequest.cs
public sealed record CreateRuleRequest(
    string From, 
    string To, 
    string Action,
    int? Priority = null
) : IMessagePayload;

// Ufw.Pipes.Shared/Model/Responses/Domain/CreateRuleResponse.cs  
public sealed record CreateRuleResponse(
    bool Success, 
    string? RuleId = null,
    string? ErrorMessage = null
) : IResponseMessage;
```

#### 2. Implement Controller Method
Add controller methods with HTTP verb attributes in `Ufw.Systemd/Api/Controllers/`:

```csharp
[Route("api/v1/rules")]
internal sealed class RulesController : ControllerBase
{
    [Post("create")]
    public async ValueTask<CreateRuleResponse> CreateRuleAsync(
        CreateRuleRequest request, 
        CancellationToken cancellationToken)
    {
        // UFW command execution logic
        // Validation, error handling, etc.
        return new CreateRuleResponse(Success: true, RuleId: "rule_123");
    }
}
```

#### 3. Register Controller (if new)
Add controller registration in `Ufw.Systemd/Api/UfwApiEndpointMap.cs`:

```csharp
[ApiControllerRegistration<RulesController>]     // ← Existing controllers
[ApiControllerRegistration<NetworkController>]   // ← Add new controllers here
[ApiControllerMappingGenerator<UfwApiEndpointMappingFactory, IMessage, IMessage>]
internal sealed partial class UfwApiEndpointMap : ApiEndpointMap<IMessage, IMessage>
```

#### 4. Source Generator Automation
- **Automatic**: Source generator (`ApiEndpointBindingGenerator`) detects controllers and HTTP attributes
- **Generated**: Endpoint mappings are created at compile time
- **Result**: Zero-runtime overhead routing with full type safety

#### 5. Implement Web Service Methods
Add client-side methods in `Ufw.Web/Services/`:

```csharp
internal sealed class UfwRuleService(IUfwClient ufwClient) : IUfwRuleService
{
    public async Task<CreateRuleResponse> CreateRuleAsync(CreateRuleRequest request)
    {
        return await ufwClient.SendAsync<CreateRuleRequest, CreateRuleResponse>(
            RequestMethod.Post, "/api/v1/rules/create", request);
    }
}
```

#### 6. Update Web UI
Integrate with Razor Pages and controllers in `Ufw.Web/Pages/` or `Ufw.Web/Areas/`.

**Key Benefits**:
- **Type Safety**: Compile-time validation of request/response contracts
- **Code Generation**: Automatic endpoint mapping without manual registration
- **Consistency**: HTTP-style semantics over secure named pipes
- **Testability**: Interface-based design enables easy unit testing

### Adding Rule Normalizers
1. Implement `IRuleNormalizer` with `Priority` property
2. Register as scoped service in `Program.cs`
3. Automatically included in normalization pipeline

This architecture provides a secure, maintainable foundation for firewall management while leveraging modern .NET features like source generators and AOT compilation for optimal performance and developer experience.