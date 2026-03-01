# Transform Pipeline Architecture

This document describes the standardized transformation pipeline architecture for the PathTransform tool system.

---

## Table of Contents

1. [Overview](#overview)
2. [File Structure](#file-structure)
3. [Pipeline Stages](#pipeline-stages)
4. [Core Types](#core-types)
5. [Transform Interface](#transform-interface)
6. [Transform Implementations](#transform-implementations)
7. [Handle Integration](#handle-integration)
8. [Data Flow](#data-flow)
9. [Usage Examples](#usage-examples)

---

## Overview

The PathTransform pipeline processes a selected path of network edges, applying shape (XZ) and slope (Y) transformations. The architecture prioritizes:

- **Burst compatibility** - All transform logic uses structs and is compatible with Unity's Burst compiler
- **Standardization** - All transforms implement the same interface with consistent lifecycle methods
- **Testability** - Pure calculation logic separated from ECS concerns
- **Extensibility** - Easy to add new transform types

### Design Principles

1. **Interface as Generic Constraint** - Transforms implement `IPathTransformation` but are dispatched explicitly (no runtime polymorphism)
2. **Three-Phase Processing** - PreProcess → Process loop → PostProcess
3. **Pipeline-Owned Sanitization** - Geometry recalculation happens after each transform, not within transforms
4. **Centralized Handle Management** - Tool system owns handle lifecycle, transforms define handle requirements

---

## File Structure

```
NetworkTools.Mod/
└── Systems/
    └── Tools/
        └── PathTransform/
            │
            ├── docs/
            │   └── TRANSFORM_PIPELINE_ARCHITECTURE.md  # This document
            │
            ├── Core/
            │   ├── IPathTransformation.cs              # Transform interface
            │   ├── TransformContext.cs                 # Path-level immutable context
            │   ├── EdgeTransformState.cs               # Per-edge mutable state
            │   ├── TransformPipeline.cs                # Pipeline executor
            │   └── TransformHandleKeys.cs              # Handle key constants
            │
            ├── Config/
            │   ├── TransformConfig.cs                  # Combined config
            │   ├── ShapeCurveConfig.cs                 # Shape config + template enum
            │   └── SlopeCurveConfig.cs                 # Slope config + template enum
            │
            ├── Transforms/
            │   ├── Shape/
            │   │   ├── StraightenTransform.cs          # Straighten implementation
            │   │   └── SmoothTransform.cs              # Smooth implementation
            │   │
            │   └── Slope/
            │       ├── LinearSlopeTransform.cs         # Linear slope implementation
            │       ├── EaseInOutSlopeTransform.cs      # Ease-in-out implementation
            │       └── ParabolicSlopeTransform.cs      # Parabolic implementation
            │
            ├── Calculators/
            │   ├── ShapeCalculator.cs                  # XZ calculation utilities
            │   ├── SlopeCalculator.cs                  # Y calculation utilities
            │   ├── EdgePositions.cs                    # XZ position result struct
            │   └── EdgeControlPointHeights.cs          # Y height result struct
            │
            ├── Intersection/
            │   ├── IntersectionEdgeAdjustment.cs       # Neighbor adjustment data
            │   └── IntersectionAdjustmentGatherer.cs   # Neighbor adjustment logic
            │
            ├── NT_PathTransformToolSystem.cs           # Main tool system (partial)
            ├── NT_PathTransformToolSystem.Lifecycle.cs # OnCreate, OnDestroy, etc.
            ├── NT_PathTransformToolSystem.Selection.cs # Node selection logic
            ├── NT_PathTransformToolSystem.Jobs.cs      # Job definitions
            ├── NT_PathTransformToolSystem.Handles.cs   # Handle creation/management
            └── NT_PathTransformToolSystem.Output.cs    # Preview/Apply output logic
```

### Migration Notes

Files to **move** (rename path):
- `EdgeTransformState.cs` → `Core/EdgeTransformState.cs` (also extract `TransformContext` to its own file)
- `TransformConfig.cs` → `Config/TransformConfig.cs`
- `ShapeCurveConfig.cs` → `Config/ShapeCurveConfig.cs`
- `SlopeCurveConfig.cs` → `Config/SlopeCurveConfig.cs`
- `ShapeCalculator.cs` → `Calculators/ShapeCalculator.cs`
- `SlopeCalculator.cs` → `Calculators/SlopeCalculator.cs`
- `IntersectionEdgeAdjustment.cs` → `Intersection/IntersectionEdgeAdjustment.cs`

Files to **create**:
- `Core/IPathTransformation.cs`
- `Core/TransformContext.cs` (extracted from EdgeTransformState.cs)
- `Core/TransformPipeline.cs`
- `Core/TransformHandleKeys.cs`
- `Transforms/Shape/StraightenTransform.cs`
- `Transforms/Shape/SmoothTransform.cs`
- `Transforms/Slope/LinearSlopeTransform.cs`
- `Transforms/Slope/EaseInOutSlopeTransform.cs`
- `Transforms/Slope/ParabolicSlopeTransform.cs`
- `Intersection/IntersectionAdjustmentGatherer.cs`
- `NT_PathTransformToolSystem.Handles.cs`

Files to **refactor**:
- `PathTransformUtility.cs` → Delete after migrating logic to transform structs
- `NT_PathTransformToolSystem.Jobs.cs` → Simplify to use `TransformPipeline`
- `NT_PathTransformToolSystem.JobMethods.cs` → Merge into main file or Output partial

---

## Pipeline Stages

The transformation pipeline executes in the following order:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PathTransformJob.Execute()                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  1. INITIALIZE CONTEXT                                                       │
│     └── Create TransformContext from path endpoints                          │
│                                                                              │
│  2. GATHER EDGE STATES                                                       │
│     └── Populate NativeArray<EdgeTransformState> with edge data              │
│     └── Calculate initial cumulative distances                               │
│     └── Store original positions for delta calculations                      │
│                                                                              │
│  3. EXECUTE TRANSFORMS (for each active transform)                           │
│     ┌─────────────────────────────────────────────────────────────────┐     │
│     │  a. PreProcess(ref edges, ref context)                          │     │
│     │     └── Global calculations (e.g., master bezier for Smooth)    │     │
│     │                                                                  │     │
│     │  b. Process loop                                                 │     │
│     │     for each edge:                                               │     │
│     │       └── Process(ref edge, index, in context)                   │     │
│     │                                                                  │     │
│     │  c. PostProcess(ref edges, ref context)                          │     │
│     │     └── Any cleanup or cross-edge adjustments                    │     │
│     │                                                                  │     │
│     │  d. RecalculateGeometry(edges, ref context)  [PIPELINE OWNED]    │     │
│     │     └── Update lengths, cumulative distances                     │     │
│     └─────────────────────────────────────────────────────────────────┘     │
│                                                                              │
│  4. GATHER INTERSECTION ADJUSTMENTS                                          │
│     └── Calculate deltas for non-path edges at intersection nodes            │
│                                                                              │
│  5. OUTPUT                                                                   │
│     └── Preview: Create CreationDefinition + NetCourse entities              │
│     └── Apply: Modify Curve components, mark entities Updated                │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Core Types

### TransformContext

Path-level immutable context created once per pipeline execution.

```csharp
/// <summary>
/// Path-level context data for the transformation pipeline.
/// Immutable after initialization - contains input configuration and derived values.
/// </summary>
public struct TransformContext {
    // === Input (immutable) ===
    
    /// <summary>Full 3D position of the path start node.</summary>
    public float3 StartPosition;
    
    /// <summary>Full 3D position of the path end node.</summary>
    public float3 EndPosition;
    
    /// <summary>The transformation configuration.</summary>
    public TransformConfig Config;
    
    // === Derived (updated by pipeline) ===
    
    /// <summary>Total length of all edges in the path.</summary>
    public float TotalLength;
    
    // === Convenience Accessors ===
    
    public float StartHeight => StartPosition.y;
    public float DeltaHeight => EndPosition.y - StartPosition.y;
    public float2 StartXZ => new(StartPosition.x, StartPosition.z);
    public float2 EndXZ => new(EndPosition.x, EndPosition.z);
    public bool IsValid => TotalLength > 0f;
    
    public static TransformContext Create(float3 startPos, float3 endPos, TransformConfig config);
}
```

### EdgeTransformState

Per-edge state that flows through the pipeline. Contains both immutable identity data and mutable geometry.

```csharp
/// <summary>
/// Per-edge state that flows through the transformation pipeline.
/// </summary>
public struct EdgeTransformState {
    // === Identity (immutable after creation) ===
    
    public Entity EdgeEntity;
    public Entity StartNode;
    public Entity EndNode;
    public int PathIndex;
    public bool IsForward;
    public NetworkComposition NetworkComposition;
    
    // === Original Values (immutable - for intersection delta calculations) ===
    
    public float OriginalEndHeight;
    public float2 OriginalEndXZ;
    
    // === Geometry (mutable - updated by transforms) ===
    
    public Bezier4x3 Bezier;
    public float Length;
    public float CumulativeDistance;
    public float ControlPointStartRatio;
    public float ControlPointEndRatio;
    
    // === Methods ===
    
    public void CalculateLength();
    public void RecalculateControlPointRatios();
    public void SetEvenControlPointRatios();
}
```

---

## Transform Interface

All transforms implement `IPathTransformation`:

```csharp
/// <summary>
/// Interface for path transformation operations.
/// Implemented by structs for Burst compatibility.
/// </summary>
public interface IPathTransformation {
    // === Metadata ===
    
    /// <summary>Whether this transform needs PreProcess called.</summary>
    bool RequiresPreProcess { get; }
    
    /// <summary>Whether this transform needs PostProcess called.</summary>
    bool RequiresPostProcess { get; }
    
    // === Lifecycle ===
    
    /// <summary>
    /// Called before processing edges. Use for global calculations
    /// that require access to all edges (e.g., calculating master bezier).
    /// </summary>
    /// <param name="edges">All edges in the path.</param>
    /// <param name="ctx">The transform context (may be modified).</param>
    void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx);
    
    /// <summary>
    /// Called for each edge in sequence. The main transformation logic.
    /// </summary>
    /// <param name="edge">The edge to transform (modified in place).</param>
    /// <param name="index">Index of this edge in the path.</param>
    /// <param name="ctx">The transform context (read-only).</param>
    void Process(ref EdgeTransformState edge, int index, in TransformContext ctx);
    
    /// <summary>
    /// Called after all edges are processed. Use for cleanup or
    /// cross-edge adjustments.
    /// </summary>
    /// <param name="edges">All edges in the path.</param>
    /// <param name="ctx">The transform context (read-only).</param>
    void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx);
}
```

### Transform Handle Requirements (Optional Extension)

Transforms that support in-world handles can implement additional metadata:

```csharp
/// <summary>
/// Optional interface for transforms that support in-world handles.
/// </summary>
public interface IHandleableTransformation : IPathTransformation {
    /// <summary>
    /// Gets the handle definitions for this transform.
    /// Called by the tool system when creating handles.
    /// </summary>
    /// <param name="ctx">The transform context.</param>
    /// <param name="pathStartPos">World position of path start.</param>
    /// <param name="pathEndPos">World position of path end.</param>
    /// <returns>Array of handle definitions.</returns>
    TransformHandleDefinition[] GetHandleDefinitions(
        in TransformContext ctx,
        float3 pathStartPos,
        float3 pathEndPos);
}

/// <summary>
/// Definition for a transform handle.
/// </summary>
public struct TransformHandleDefinition {
    public int Key;
    public float3 Position;
    public HandleTypeFlags TypeFlags;
    public float Value;        // For parameter handles
    public float MinValue;
    public float MaxValue;
    public NT_HandleConstraints? Constraints;
}
```

---

## Transform Implementations

### Shape Transforms

#### StraightenTransform

```csharp
/// <summary>
/// Straightens all edges to lie on a direct line from path start to path end.
/// </summary>
public struct StraightenTransform : IPathTransformation {
    public ShapeCurveConfig Config;
    
    public bool RequiresPreProcess => false;
    public bool RequiresPostProcess => false;
    
    public void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx) { }
    
    public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
        var positions = ShapeCalculator.CalculateStraightenedPositions(in edge, in ctx);
        edge.Bezier = ShapeCalculator.ApplyPositionsToBezier(edge.Bezier, positions, edge.IsForward);
        edge.SetEvenControlPointRatios();
    }
    
    public void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx) { }
}
```

#### SmoothTransform

```csharp
/// <summary>
/// Smooths all edges to follow a master bezier curve from path start to path end.
/// </summary>
public struct SmoothTransform : IPathTransformation, IHandleableTransformation {
    public ShapeCurveConfig Config;
    
    // Computed in PreProcess
    private float2 m_MasterCtrl1;
    private float2 m_MasterCtrl2;
    
    public bool RequiresPreProcess => true;
    public bool RequiresPostProcess => false;
    
    public void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx) {
        if (edges.Length == 0) return;
        
        var firstEdge = edges[0];
        var lastEdge = edges[^1];
        
        var startTangent = ShapeCalculator.GetBezierTangentXZ(firstEdge.Bezier, true, firstEdge.IsForward);
        var endTangent = ShapeCalculator.GetBezierTangentXZ(lastEdge.Bezier, false, lastEdge.IsForward);
        
        ShapeCalculator.CalculateMasterBezierControls(
            ctx.StartXZ, ctx.EndXZ,
            startTangent, endTangent,
            ctx.TotalLength,
            out m_MasterCtrl1, out m_MasterCtrl2);
    }
    
    public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
        var positions = ShapeCalculator.CalculateSmoothedPositions(
            in edge, in ctx, m_MasterCtrl1, m_MasterCtrl2);
        edge.Bezier = ShapeCalculator.ApplyPositionsToBezier(edge.Bezier, positions, edge.IsForward);
    }
    
    public void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx) { }
    
    public TransformHandleDefinition[] GetHandleDefinitions(
        in TransformContext ctx, float3 pathStartPos, float3 pathEndPos) {
        
        // Master bezier control point handles
        var ctrl1Pos = new float3(m_MasterCtrl1.x, pathStartPos.y, m_MasterCtrl1.y);
        var ctrl2Pos = new float3(m_MasterCtrl2.x, pathEndPos.y, m_MasterCtrl2.y);
        
        return new[] {
            new TransformHandleDefinition {
                Key = TransformHandleKeys.SmoothCtrl1,
                Position = ctrl1Pos,
                TypeFlags = HandleTypeFlags.ShapeControl | HandleTypeFlags.BezierControlPoint,
            },
            new TransformHandleDefinition {
                Key = TransformHandleKeys.SmoothCtrl2,
                Position = ctrl2Pos,
                TypeFlags = HandleTypeFlags.ShapeControl | HandleTypeFlags.BezierControlPoint,
            },
        };
    }
}
```

### Slope Transforms

#### LinearSlopeTransform

```csharp
/// <summary>
/// Applies a linear slope - constant gradient throughout the path.
/// </summary>
public struct LinearSlopeTransform : IPathTransformation {
    public SlopeCurveConfig Config;
    
    public bool RequiresPreProcess => false;
    public bool RequiresPostProcess => false;
    
    public void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx) { }
    
    public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
        // Linear slopes use even control point ratios for constant gradient
        edge.SetEvenControlPointRatios();
        
        var heights = SlopeCalculator.CalculateEdgeHeights(in edge, in ctx);
        edge.Bezier = SlopeCalculator.ApplyHeightsToBezier(edge.Bezier, heights, edge.IsForward);
    }
    
    public void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx) { }
}
```

#### EaseInOutSlopeTransform

```csharp
/// <summary>
/// Applies an ease-in-out slope - smooth transitions at start and end.
/// </summary>
public struct EaseInOutSlopeTransform : IPathTransformation, IHandleableTransformation {
    public SlopeCurveConfig Config;
    
    public bool RequiresPreProcess => false;
    public bool RequiresPostProcess => false;
    
    public void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx) { }
    
    public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
        var heights = SlopeCalculator.CalculateEdgeHeights(in edge, in ctx);
        edge.Bezier = SlopeCalculator.ApplyHeightsToBezier(edge.Bezier, heights, edge.IsForward);
    }
    
    public void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx) { }
    
    public TransformHandleDefinition[] GetHandleDefinitions(
        in TransformContext ctx, float3 pathStartPos, float3 pathEndPos) {
        
        // Ease length parameter handles
        var easeInPos = math.lerp(pathStartPos, pathEndPos, Config.EaseInLength);
        var easeOutPos = math.lerp(pathEndPos, pathStartPos, Config.EaseOutLength);
        
        return new[] {
            new TransformHandleDefinition {
                Key = TransformHandleKeys.EaseInLength,
                Position = easeInPos,
                TypeFlags = HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter,
                Value = Config.EaseInLength,
                MinValue = 0f,
                MaxValue = 0.5f,
            },
            new TransformHandleDefinition {
                Key = TransformHandleKeys.EaseOutLength,
                Position = easeOutPos,
                TypeFlags = HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter,
                Value = Config.EaseOutLength,
                MinValue = 0f,
                MaxValue = 0.5f,
            },
        };
    }
}
```

#### ParabolicSlopeTransform

```csharp
/// <summary>
/// Applies a parabolic slope - creates an arch (hill) or dip (valley).
/// </summary>
public struct ParabolicSlopeTransform : IPathTransformation, IHandleableTransformation {
    public SlopeCurveConfig Config;
    
    public bool RequiresPreProcess => false;
    public bool RequiresPostProcess => false;
    
    public void PreProcess(ref NativeArray<EdgeTransformState> edges, ref TransformContext ctx) { }
    
    public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
        var heights = SlopeCalculator.CalculateEdgeHeights(in edge, in ctx);
        edge.Bezier = SlopeCalculator.ApplyHeightsToBezier(edge.Bezier, heights, edge.IsForward);
    }
    
    public void PostProcess(ref NativeArray<EdgeTransformState> edges, in TransformContext ctx) { }
    
    public TransformHandleDefinition[] GetHandleDefinitions(
        in TransformContext ctx, float3 pathStartPos, float3 pathEndPos) {
        
        // Arch position and height handles
        var archXZ = math.lerp(ctx.StartXZ, ctx.EndXZ, Config.ArchPosition);
        var baseHeight = math.lerp(ctx.StartHeight, ctx.StartHeight + ctx.DeltaHeight, Config.ArchPosition);
        var archPos = new float3(archXZ.x, baseHeight + Config.ArchHeight * 10f, archXZ.y);
        
        return new[] {
            new TransformHandleDefinition {
                Key = TransformHandleKeys.ArchPosition,
                Position = archPos,
                TypeFlags = HandleTypeFlags.SlopeControl | HandleTypeFlags.Position,
                Constraints = NT_HandleConstraints.AxisOnly(new float3(0, 1, 0)), // Y-axis only
            },
        };
    }
}
```

---

## Handle Integration

### Handle Keys

```csharp
/// <summary>
/// Constants for transform handle identification.
/// Used in NT_HandleLink.Key to map handles to config parameters.
/// </summary>
public static class TransformHandleKeys {
    // Shape handles (100-199)
    public const int SmoothCtrl1 = 100;
    public const int SmoothCtrl2 = 101;
    public const int SmoothingFactor = 102;
    
    // Slope handles (200-299)
    public const int EaseInLength = 200;
    public const int EaseOutLength = 201;
    public const int ArchHeight = 210;
    public const int ArchPosition = 211;
}
```

### Handle Lifecycle in Tool System

```csharp
// NT_PathTransformToolSystem.Handles.cs

public partial class NT_PathTransformToolSystem {
    
    /// <summary>
    /// Creates handles for all active transforms.
    /// Called when entering OperationPhase.Ready.
    /// </summary>
    private void CreateTransformHandles() {
        DestroyAllHandles();
        
        var pathStartPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[0]).m_Position;
        var pathEndPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[^1]).m_Position;
        
        if (TransformConfig.HasShapeTransform) {
            CreateShapeHandles(pathStartPos, pathEndPos);
        }
        
        if (TransformConfig.HasSlopeTransform) {
            CreateSlopeHandles(pathStartPos, pathEndPos);
        }
    }
    
    private void CreateShapeHandles(float3 pathStartPos, float3 pathEndPos) {
        switch (TransformConfig.Shape.Template) {
            case ShapeTemplate.Smooth:
                // Create smooth transform and get handle definitions
                var smoothTransform = new SmoothTransform { Config = TransformConfig.Shape };
                // Note: Would need to run PreProcess to get control points
                // This is a design consideration - may need cached control points
                break;
        }
    }
    
    private void CreateSlopeHandles(float3 pathStartPos, float3 pathEndPos) {
        switch (TransformConfig.Slope.Template) {
            case SlopeTemplate.EaseInOut:
                var easeInPos = math.lerp(pathStartPos, pathEndPos, TransformConfig.Slope.EaseInLength);
                var easeOutPos = math.lerp(pathEndPos, pathStartPos, TransformConfig.Slope.EaseOutLength);
                
                CreateParameterHandle(
                    Entity.Null,
                    TransformHandleKeys.EaseInLength,
                    easeInPos + new float3(0, 3, 0),
                    TransformConfig.Slope.EaseInLength,
                    0f, 0.5f,
                    HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter);
                    
                CreateParameterHandle(
                    Entity.Null,
                    TransformHandleKeys.EaseOutLength,
                    easeOutPos + new float3(0, 3, 0),
                    TransformConfig.Slope.EaseOutLength,
                    0f, 0.5f,
                    HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter);
                break;
                
            case SlopeTemplate.Parabolic:
                var archT = TransformConfig.Slope.ArchPosition;
                var archXZ = math.lerp(pathStartPos.xz, pathEndPos.xz, archT);
                var baseY = math.lerp(pathStartPos.y, pathEndPos.y, archT);
                var archPos = new float3(archXZ.x, baseY + TransformConfig.Slope.ArchHeight * 10f, archXZ.y);
                
                CreatePositionHandle(
                    Entity.Null, Entity.Null,
                    TransformHandleKeys.ArchPosition,
                    archPos,
                    HandleTypeFlags.SlopeControl | HandleTypeFlags.Position,
                    NT_HandleConstraints.AxisOnly(new float3(0, 1, 0)));
                break;
        }
    }
    
    protected override void OnHandleDragging(Entity handle) {
        var link = EntityManager.GetComponentData<NT_HandleLink>(handle);
        
        switch (link.Key) {
            case TransformHandleKeys.EaseInLength:
                var easeInValue = EntityManager.GetComponentData<NT_HandleValue>(handle);
                TransformConfig.Slope.EaseInLength = easeInValue.Value;
                break;
                
            case TransformHandleKeys.EaseOutLength:
                var easeOutValue = EntityManager.GetComponentData<NT_HandleValue>(handle);
                TransformConfig.Slope.EaseOutLength = easeOutValue.Value;
                break;
                
            case TransformHandleKeys.ArchPosition:
                // Map handle Y position to arch height
                var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handle).Position;
                var pathStartPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[0]).m_Position;
                var pathEndPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[^1]).m_Position;
                var baseY = math.lerp(pathStartPos.y, pathEndPos.y, TransformConfig.Slope.ArchPosition);
                TransformConfig.Slope.ArchHeight = (handlePos.y - baseY) / 10f;
                break;
        }
        
        m_UpdateNeeded = true;
    }
}
```

---

## Data Flow

### Pipeline Execution Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                        INPUT                                      │
│  • m_SelectedNodes: NativeList<Entity>                           │
│  • m_CurrentPathEdges: NativeList<Entity>                        │
│  • m_CurrentPathNodes: NativeList<Entity>                        │
│  • TransformConfig: TransformConfig                              │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                    INITIALIZE CONTEXT                             │
│                                                                   │
│  TransformContext ctx = TransformContext.Create(                 │
│      startPos: NodeLookup[m_SelectedNodes[0]].m_Position,        │
│      endPos: NodeLookup[m_SelectedNodes[^1]].m_Position,         │
│      config: TransformConfig                                      │
│  );                                                               │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                     GATHER EDGE STATES                            │
│                                                                   │
│  NativeArray<EdgeTransformState> edges = GatherEdgeStates();     │
│                                                                   │
│  For each edge:                                                   │
│    • Copy Entity references (EdgeEntity, StartNode, EndNode)      │
│    • Copy Bezier from Curve component                            │
│    • Calculate Length, IsForward, PathIndex                       │
│    • Store OriginalEndHeight, OriginalEndXZ (for deltas)          │
│    • Accumulate CumulativeDistance                                │
│                                                                   │
│  ctx.TotalLength = sum of all edge lengths                       │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                    EXECUTE TRANSFORMS                             │
│                                                                   │
│  // Shape transform (if active)                                   │
│  if (ctx.Config.HasShapeTransform) {                             │
│      var transform = CreateShapeTransform(ctx.Config.Shape);      │
│      TransformPipeline.Execute(transform, ref edges, ref ctx);    │
│  }                                                                │
│                                                                   │
│  // Slope transform (if active)                                   │
│  if (ctx.Config.HasSlopeTransform) {                             │
│      var transform = CreateSlopeTransform(ctx.Config.Slope);      │
│      TransformPipeline.Execute(transform, ref edges, ref ctx);    │
│  }                                                                │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│               GATHER INTERSECTION ADJUSTMENTS                     │
│                                                                   │
│  For each intersection node in path:                              │
│    • Calculate height delta: newHeight - originalHeight           │
│    • Calculate XZ delta: newXZ - originalXZ                       │
│    • For each non-path edge connected to this node:               │
│      • Adjust endpoint and adjacent control point                 │
│      • Create IntersectionEdgeAdjustment                          │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                          OUTPUT                                   │
│                                                                   │
│  Preview Mode:                                                    │
│    • Create CreationDefinition entity for each edge               │
│    • Add NetCourse component with transformed bezier              │
│    • Reference far nodes, leave path nodes as Entity.Null         │
│                                                                   │
│  Apply Mode:                                                      │
│    • Set Curve component on each edge entity                      │
│    • Add Updated/BatchesUpdated components                        │
│    • Update Node positions (via connected edge updates)           │
└──────────────────────────────────────────────────────────────────┘
```

### Transform Pipeline Executor

```csharp
/// <summary>
/// Executes a single transformation through all phases.
/// </summary>
public static class TransformPipeline {
    
    public static void Execute<T>(
        T transform,
        ref NativeArray<EdgeTransformState> edges,
        ref TransformContext ctx)
        where T : struct, IPathTransformation {
        
        // 1. PreProcess (global calculations)
        if (transform.RequiresPreProcess) {
            transform.PreProcess(ref edges, ref ctx);
        }
        
        // 2. Process each edge
        for (var i = 0; i < edges.Length; i++) {
            var edge = edges[i];
            transform.Process(ref edge, i, in ctx);
            edges[i] = edge;
        }
        
        // 3. PostProcess (cleanup)
        if (transform.RequiresPostProcess) {
            transform.PostProcess(ref edges, in ctx);
        }
        
        // 4. Sanitize - always recalculate geometry after each transform
        RecalculateGeometry(ref edges, ref ctx);
    }
    
    /// <summary>
    /// Recalculates edge lengths and cumulative distances.
    /// Called after each transform to ensure consistent state.
    /// </summary>
    private static void RecalculateGeometry(
        ref NativeArray<EdgeTransformState> edges,
        ref TransformContext ctx) {
        
        var cumulativeDistance = 0f;
        
        for (var i = 0; i < edges.Length; i++) {
            var edge = edges[i];
            edge.CalculateLength();
            edge.CumulativeDistance = cumulativeDistance;
            edges[i] = edge;
            cumulativeDistance += edge.Length;
        }
        
        ctx.TotalLength = cumulativeDistance;
    }
}
```

---

## Usage Examples

### Basic Job Structure

```csharp
[BurstCompile]
internal struct PathTransformJob : IJob {
    [ReadOnly] public NativeList<Entity> SelectedNodes;
    [ReadOnly] public NativeList<Entity> CurrentPathEdges;
    [ReadOnly] public NativeList<Entity> CurrentPathNodes;
    [ReadOnly] public ComponentLookup<Node> NodeLookup;
    [ReadOnly] public ComponentLookup<Curve> CurveLookup;
    [ReadOnly] public ComponentLookup<Edge> EdgeLookup;
    [ReadOnly] public TransformConfig Config;
    
    public TransformOutputMode OutputMode;
    public EntityCommandBuffer ECB;
    
    public void Execute() {
        // 1. Initialize context
        var ctx = TransformContext.Create(
            NodeLookup[SelectedNodes[0]].m_Position,
            NodeLookup[SelectedNodes[^1]].m_Position,
            Config);
        
        // 2. Gather edge states
        var edges = GatherEdgeStates(ref ctx);
        if (edges.Length == 0) {
            edges.Dispose();
            return;
        }
        
        // 3. Execute shape transform
        if (ctx.Config.HasShapeTransform) {
            ExecuteShapeTransform(ref edges, ref ctx);
        }
        
        // 4. Execute slope transform
        if (ctx.Config.HasSlopeTransform) {
            ExecuteSlopeTransform(ref edges, ref ctx);
        }
        
        // 5. Gather intersection adjustments
        var adjustments = GatherIntersectionAdjustments(edges, in ctx);
        
        // 6. Output
        Output(edges, adjustments, in ctx);
        
        adjustments.Dispose();
        edges.Dispose();
    }
    
    private void ExecuteShapeTransform(
        ref NativeArray<EdgeTransformState> edges,
        ref TransformContext ctx) {
        
        switch (ctx.Config.Shape.Template) {
            case ShapeTemplate.Straighten:
                var straighten = new StraightenTransform { Config = ctx.Config.Shape };
                TransformPipeline.Execute(straighten, ref edges, ref ctx);
                break;
                
            case ShapeTemplate.Smooth:
                var smooth = new SmoothTransform { Config = ctx.Config.Shape };
                TransformPipeline.Execute(smooth, ref edges, ref ctx);
                break;
        }
    }
    
    private void ExecuteSlopeTransform(
        ref NativeArray<EdgeTransformState> edges,
        ref TransformContext ctx) {
        
        switch (ctx.Config.Slope.Template) {
            case SlopeTemplate.Linear:
                var linear = new LinearSlopeTransform { Config = ctx.Config.Slope };
                TransformPipeline.Execute(linear, ref edges, ref ctx);
                break;
                
            case SlopeTemplate.EaseInOut:
                var easeInOut = new EaseInOutSlopeTransform { Config = ctx.Config.Slope };
                TransformPipeline.Execute(easeInOut, ref edges, ref ctx);
                break;
                
            case SlopeTemplate.Parabolic:
                var parabolic = new ParabolicSlopeTransform { Config = ctx.Config.Slope };
                TransformPipeline.Execute(parabolic, ref edges, ref ctx);
                break;
        }
    }
}
```

### Adding a New Transform

To add a new transform (e.g., `SinewaveTransform`):

1. **Create config enum value** in `ShapeCurveConfig.cs` or `SlopeCurveConfig.cs`:
   ```csharp
   public enum ShapeTemplate {
       Preserve = 0,
       Straighten = 1,
       Smooth = 2,
       Sinewave = 3,  // NEW
   }
   ```

2. **Create transform struct** in `Transforms/Shape/SinewaveTransform.cs`:
   ```csharp
   public struct SinewaveTransform : IPathTransformation {
       public ShapeCurveConfig Config;
       
       public bool RequiresPreProcess => false;
       public bool RequiresPostProcess => false;
       
       public void PreProcess(...) { }
       
       public void Process(ref EdgeTransformState edge, int index, in TransformContext ctx) {
           // Your transformation logic
       }
       
       public void PostProcess(...) { }
   }
   ```

3. **Add dispatch case** in `PathTransformJob.ExecuteShapeTransform()`:
   ```csharp
   case ShapeTemplate.Sinewave:
       var sinewave = new SinewaveTransform { Config = ctx.Config.Shape };
       TransformPipeline.Execute(sinewave, ref edges, ref ctx);
       break;
   ```

4. **(Optional) Add handles** if the transform has configurable parameters.

---

## Summary

| Aspect | Decision |
|--------|----------|
| Architecture | Interface as generic constraint + explicit dispatch |
| Processing Model | Hybrid: PreProcess/PostProcess = array, Process = single edge |
| Number of Passes | Three (Pre/Process/Post) |
| Neighbor Handling | Separate pipeline stage after all transforms |
| Config Ownership | External, passed into transform structs |
| Handle Creation | On entering `OperationPhase.Ready` |
| Handle-Config Mapping | `NT_HandleLink.Key` as parameter identifier |
| Multiple Handle Sets | Yes, with visual differentiation by type flags |
| Handle Ownership | Tool system manages centrally |
| Sanitization | Pipeline handles after each transform |

---

## Related Documents

- [HANDLE_SYSTEM_ARCHITECTURE.md](../../docs/HANDLE_SYSTEM_ARCHITECTURE.md) - Handle system design
- [NT_PathTransformToolSystem.cs](../NT_PathTransformToolSystem.cs) - Main tool system

