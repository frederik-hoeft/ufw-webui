# UFW WebUI - AI Coding Assistant Instructions

## Project Overview
This is a UFW (Uncomplicated Firewall) web management interface with a multi-project .NET 9 architecture that enables secure web-based firewall rule management through a systemd service backend.

## Code Style Guidelines

- See `/code-style.md` for detailed C# coding conventions to be used across all projects.

## Architecture Components

### Core Projects Structure
- **`Ufw.Web`**: ASP.NET Core web interface with SQLite, Identity, and Razor Pages
- **`Ufw.Systemd`**: AOT-compiled console service using ConsoleAppFramework (`PublishAot=true`)  
- **`Ufw.Pipes.Shared`**: Named pipe communication layer with serialization and transport
- **`Ufw.Pipes.Client`**: Client-side pipe communication abstractions
- **`Ufw.Roslyn`** & **`Ufw.Roslyn.SourceGen`**: Source generation infrastructure

### Key Communication Pattern
The web interface communicates with the systemd service via **named pipes** for privileged UFW operations:
- Web UI → `IUfwClient` → Named Pipes → Systemd Service → UFW commands
- Service configuration loads from `/etc/ufw-manager/settings.json`

## Critical Development Patterns

### Friend Assembly Pattern
Uses `InternalsVisibleTo` in `_friends.cs` files for controlled internal access between projects:
```csharp
[assembly: InternalsVisibleTo("Ufw.Pipes.Client")]
```

### Global Usings Pattern  
Each project has `_globalusings.cs` importing shared utilities:
```csharp
global using static Ufw.Pipes.Shared.SyntacticSugar;
global using static Ufw.Pipes.Shared.Suppressions;
```

### Pipeline Normalization System
UFW rules use a priority-based normalization pipeline in `Ufw.Web.Pipeline`:
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
- Build solution: `dotnet build src/Ufw.sln`
- Run web interface: `dotnet run --project src/Ufw.Web`
- Run systemd service: `dotnet run --project src/Ufw.Systemd serve`
- Test: `dotnet test src/Ufw.Web.Tests`

### Source Generation Integration
Projects reference `Ufw.Roslyn.SourceGen` as analyzer:
```xml
<ProjectReference Include="..\Ufw.Roslyn.SourceGen\Ufw.Roslyn.SourceGen.csproj" 
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

## File Organization Conventions
- `Models/`: Data models and DTOs
- `Services/`: Business logic with interface/implementation pairs  
- `Pipeline/`: Normalization and processing logic
- `Transport/`: Named pipe communication layer
- `Configuration/`: Service configuration and DI setup
- `Handlers/`: Message handlers for pipe communication

When adding new features, maintain the interface-first service pattern, register dependencies in `Program.cs`, and follow the established project reference hierarchy.