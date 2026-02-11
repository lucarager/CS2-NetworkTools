# NetworkTools - GitHub Copilot Instructions

## Project Overview
This is **NetworkTools**, a Cities: Skylines 2 mod providing network manipulation tools (Add Node, Remove Node, Slope tools) for players wishing to modify and refine their road networks. 
The mod uses the CS2 modding SDK with Unity ECS (Entity Component System) architecture.

## Technology Stack
- **Framework**: .NET Framework 4.8
- **Language**: C# 11.0
- **Game Engine**: Unity (via Cities: Skylines 2 SDK)
- **Architecture**: Unity ECS (Entity Component System)
- **Patching**: Harmony for runtime patching
- **UI**: Colossal UI with TypeScript/React frontend in `UI/` folder

## Code Style Guidelines

### Namespace & Using Statements
- Use `#region Using Statements` to wrap using directives
- Place namespace opening brace on same line as namespace declaration
- Example:
```csharp
namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Tools;
    using Unity.Entities;

    #endregion

    public class MySystem { }
}
```

### Naming Conventions
- **Classes/Systems**: Prefix with `NT_` (e.g., `NT_BaseToolSystem`, `NT_RemoveNodeToolSystem`)
- **Components**: Prefix with `NT_` (e.g., `NT_Eligible`, `NT_Selected`, `NT_Highlighted`)
- **Private fields**: Use `m_` prefix (e.g., `m_Log`, `m_Prefab`, `m_ToolSystem`)
- **Public fields**: PascalCase (e.g., `ShowNodes`, `ShowEdges`)
- **Constants**: PascalCase or UPPER_CASE for string constants

### Formatting
- **Indentation**: 4 spaces for C# files
- **Braces**: Opening brace on same line for namespaces, classes, and methods
- **Single-line methods**: Allowed for simple getters/setters
```csharp
public override PrefabBase GetPrefab() { return m_Prefab; }
```
- **Alignment**: Align multiple variable declarations when it improves readability
```csharp
private  PrefabBase       m_Prefab;
internal PrefixedLogger   m_Log;
private  ValidationSystem m_ValidationSystem;
```

### Documentation
- Use XML documentation comments (`///`) for public APIs
- Use `<summary>` tags for class and method descriptions
- Document parameters with `<param>` tags

### ECS Patterns
- Systems inherit from `NT_BaseToolSystem` or game base systems
- Components are structs implementing `IComponentData`
- Use partial classes for large systems, split into:
  - `SystemName.cs` - Main logic
  - `SystemName.Lifecycle.cs` - OnCreate, OnDestroy, OnStartRunning, OnStopRunning
  - `SystemName.Jobs.cs` - Job struct definitions
  - `SystemName.JobMethods.cs` - Job scheduling methods

### Logging
- Use `PrefixedLogger` for module-level logging
- Initialize in `OnCreate()`: `m_Log = new PrefixedLogger(nameof(MySystem));`
- Use appropriate log levels: `Debug`, `Info`, `Warn`, `Error`

## Project Structure
```
NetworkTools/
├── Components/          # ECS components (NT_*.cs)
├── Extensions/          # Extension methods and utilities
├── Prefabs/            # Custom prefab definitions
├── Settings/           # Mod settings and localization
├── Systems/            # ECS systems
│   ├── AddNode/        # Add Node tool system
│   ├── RemoveNode/     # Remove Node tool system
│   └── Slope/          # Slope tool system
├── Utils/              # Utility classes
├── UI/                 # TypeScript/React frontend
└── NetworkToolsMod.cs  # Mod entry point (IMod)
```

## Important Classes
- `NetworkToolsMod` - Main mod entry point implementing `IMod`
- `NT_BaseToolSystem` - Base class for all tool systems
- `NT_Settings` - Mod settings configuration
- `PrefixedLogger` - Logging utility with module prefixes
