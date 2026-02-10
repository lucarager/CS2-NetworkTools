# Refactor: Unified Shape & Slope Tool

## Overview

This document outlines a refactoring plan to extend the existing **Slope Tool** (`NT_SlopeToolSystem`) to support both **slope editing** (Y-axis) and **shape editing** (XZ-axis) in a unified transformation pipeline.

### Goals
- Allow players to edit road slope (vertical alignment) and shape (horizontal alignment) independently or together
- Share the existing path selection logic between both operations
- Design for future extensibility (e.g., "maintain visual length" flags, 3D redistribution)
- Merge UI tabs into a single unified tool panel

---

## Current Architecture 

The `SlopeTransformJob` uses a mature pattern:

```csharp
// Current structure in SlopeCalculator.cs
public enum SlopeOutputMode : byte { Preview, Apply }

public struct EdgeSlopeData {
    public float Length;
    public float CtrlStartRatio;
    public float CtrlEndRatio;
    public bool  IsForward;
    public float OldHeight;  // For intersection updates
}

public struct EdgeHeights { Start, CtrlStart, CtrlEnd, End }

public struct ComputedEdgeSlope {
    public int       PathIndex;
    public Entity    EdgeEntity;
    public Entity    StartNode;
    public Entity    EndNode;
    public Bezier4x3 AdjustedBezier;
    public float     CumulativeDistance;
    public EdgeSlopeData Metadata;
}
```

The job has two output modes:
- **Preview**: Creates `CreationDefinition` + `NetCourse` entities for visual preview
- **Apply**: Modifies existing `Curve` components and handles intersection adjustments

---

## Target Architecture: Transformation Pipeline

We extend the current pattern with a **sequential transformation pipeline**:

```
Original Bezier → [Shape Transform] → [Slope Transform] → Final Bezier
```

Each transform:
- Can be a **no-op** (`Preserve` template) that keeps existing data
- Receives the **output of the previous step**
- Is applied in Phase 2 of `SlopeTransformJob` (renamed to `PathTransformJob`)

---

## New Data Structures

### 1. `ShapeTemplate` Enum & `ShapeCurveConfig` Struct

**New file: `Systems/Slope/ShapeCalculator.cs`**

```csharp
/// <summary>
/// Defines the type of shape curve to apply to road segments (XZ plane).
/// </summary>
public enum ShapeTemplate {
    Preserve = 0,      // Keep existing XZ positions (no-op)
    Straighten = 1,    // Align all nodes along a straight line between start/end
    Smooth = 2,        // Fit nodes to a smooth bezier curve
    EqualSpacing = 3,  // Redistribute nodes evenly along the path
}

/// <summary>
/// Per-edge metadata for shape calculations.
/// Mirrors EdgeSlopeData but tracks XZ positions.
/// </summary>
public struct EdgeShapeData {
    public float  Length;
    public float  CtrlStartRatio;
    public float  CtrlEndRatio;
    public bool   IsForward;
    public float2 OldPositionXZ;  // Original XZ at path-end (for intersection updates)
}

/// <summary>
/// Pre-calculated XZ positions for an edge's control points in path order.
/// Mirrors EdgeHeights structure.
/// </summary>
public struct EdgePositions {
    public float2 Start;
    public float2 CtrlStart;
    public float2 CtrlEnd;
    public float2 End;
}

/// <summary>
/// Computed shape data for a single edge, ready to be output.
/// Mirrors ComputedEdgeSlope structure.
/// </summary>
public struct ComputedEdgeShape {
    public int       PathIndex;
    public Entity    EdgeEntity;
    public Entity    StartNode;
    public Entity    EndNode;
    public Bezier4x3 AdjustedBezier;
    public float     CumulativeDistance;
    public EdgeShapeData Metadata;
}

/// <summary>
/// Configuration for shape curve application.
/// </summary>
public struct ShapeCurveConfig {
    public ShapeTemplate Template;
    
    // Smooth parameters
    public float SmoothingFactor;  // 0-1, how much to smooth
    
    public static ShapeCurveConfig Preserve() => new ShapeCurveConfig { Template = ShapeTemplate.Preserve };
    public static ShapeCurveConfig Straighten() => new ShapeCurveConfig { Template = ShapeTemplate.Straighten };
    public static ShapeCurveConfig Smooth(float factor = 0.5f) => new ShapeCurveConfig { 
        Template = ShapeTemplate.Smooth, 
        SmoothingFactor = math.clamp(factor, 0f, 1f),
    };
}
```

### 2. Update `SlopeTemplate` Enum

**Modify: `Systems/Slope/SlopeCurveConfig.cs`**

Add `Preserve = 0` as the first option:

```csharp
public enum SlopeTemplate {
    Preserve = 0,   // Keep existing Y positions (no-op) - NEW
    Linear = 1,     // Was 0
    EaseInOut = 2,  // Was 1
    Parabolic = 3,  // Was 2
}
```

> ⚠️ **Breaking Change**: Existing serialized configs will shift. Consider migration or use explicit values.

### 3. `TransformConfig` Struct

**New file: `Systems/Slope/TransformConfig.cs`**

```csharp
/// <summary>
/// Determines how the transformation job outputs its results.
/// Replaces SlopeOutputMode with a more generic name.
/// </summary>
public enum TransformOutputMode : byte {
    Preview,
    Apply,
}

/// <summary>
/// Flags for future extensibility.
/// </summary>
[Flags]
public enum TransformFlags {
    None = 0,
    // Future:
    // MaintainVisualLength = 1 << 0,
    // RedistributeNodes = 1 << 1,
}

/// <summary>
/// Unified configuration for path transformations.
/// Holds both shape (XZ) and slope (Y) settings.
/// </summary>
public struct TransformConfig {
    public ShapeCurveConfig Shape;
    public SlopeCurveConfig Slope;
    public TransformFlags   Flags;
    
    /// <summary>
    /// Whether any transformation will be applied.
    /// </summary>
    public bool HasTransform => 
        Shape.Template != ShapeTemplate.Preserve || 
        Slope.Template != SlopeTemplate.Preserve;
    
    /// <summary>
    /// Whether shape transformation is active.
    /// </summary>
    public bool HasShapeTransform => Shape.Template != ShapeTemplate.Preserve;
    
    /// <summary>
    /// Whether slope transformation is active.
    /// </summary>
    public bool HasSlopeTransform => Slope.Template != SlopeTemplate.Preserve;
    
    /// <summary>
    /// Default config that preserves everything.
    /// </summary>
    public static TransformConfig Preserve() => new TransformConfig {
        Shape = ShapeCurveConfig.Preserve(),
        Slope = SlopeCurveConfig.Preserve(),
        Flags = TransformFlags.None,
    };
    
    /// <summary>
    /// Creates a slope-only config (preserves shape).
    /// </summary>
    public static TransformConfig SlopeOnly(SlopeCurveConfig slope) => new TransformConfig {
        Shape = ShapeCurveConfig.Preserve(),
        Slope = slope,
        Flags = TransformFlags.None,
    };
    
    /// <summary>
    /// Creates a shape-only config (preserves slope).
    /// </summary>
    public static TransformConfig ShapeOnly(ShapeCurveConfig shape) => new TransformConfig {
        Shape = shape,
        Slope = SlopeCurveConfig.Preserve(),
        Flags = TransformFlags.None,
    };
}
```

### 4. Update `OperationState` Struct

**Modify: `Systems/Slope/NT_SlopeToolSystem.cs`**

```csharp
public struct OperationState {
    public OperationPhase  Phase;
    public TransformConfig Config;  // Changed from SlopeCurveConfig

    public bool CanPreview => Phase == OperationPhase.Ready;
    public bool IsActive => Phase != OperationPhase.Idle;

    public static OperationState Idle() => new OperationState {
        Phase  = OperationPhase.Idle,
        Config = TransformConfig.Preserve(),
    };
}
```

---

## New Utility Classes

### 1. `ShapeCalculator` Static Class

**New file: `Systems/Slope/ShapeCalculator.cs`** (same file as structs above)

```csharp
/// <summary>
/// Burst-compatible utility for shape (XZ) calculations.
/// Mirrors SlopeCalculator structure.
/// </summary>
public static class ShapeCalculator {
    /// <summary>
    /// Calculates XZ position at a given distance along a straight line.
    /// </summary>
    public static float2 CalculatePositionLinear(
        float  distance, 
        float  totalLength, 
        float2 startXZ, 
        float2 endXZ) {
        var ratio = math.clamp(distance / totalLength, 0f, 1f);
        return math.lerp(startXZ, endXZ, ratio);
    }

    /// <summary>
    /// Calculates XZ positions for all four bezier control points (straighten mode).
    /// </summary>
    public static EdgePositions CalculateStraightenedPositions(
        float  cumulativeDistance,
        float  edgeLength,
        float  ctrlStartRatio,
        float  ctrlEndRatio,
        float  totalLength,
        float2 pathStartXZ,
        float2 pathEndXZ) {
        var distStart     = cumulativeDistance;
        var distCtrlStart = cumulativeDistance + edgeLength * ctrlStartRatio;
        var distCtrlEnd   = cumulativeDistance + edgeLength * ctrlEndRatio;
        var distEnd       = cumulativeDistance + edgeLength;

        return new EdgePositions {
            Start     = CalculatePositionLinear(distStart, totalLength, pathStartXZ, pathEndXZ),
            CtrlStart = CalculatePositionLinear(distCtrlStart, totalLength, pathStartXZ, pathEndXZ),
            CtrlEnd   = CalculatePositionLinear(distCtrlEnd, totalLength, pathStartXZ, pathEndXZ),
            End       = CalculatePositionLinear(distEnd, totalLength, pathStartXZ, pathEndXZ),
        };
    }

    /// <summary>
    /// Applies calculated XZ positions to a bezier curve, preserving Y values.
    /// </summary>
    public static Bezier4x3 ApplyPositionsToBezier(in Bezier4x3 bezier, in EdgePositions positions, bool isForward) {
        var result = bezier;

        if (isForward) {
            result.a.x = positions.Start.x;     result.a.z = positions.Start.y;
            result.b.x = positions.CtrlStart.x; result.b.z = positions.CtrlStart.y;
            result.c.x = positions.CtrlEnd.x;   result.c.z = positions.CtrlEnd.y;
            result.d.x = positions.End.x;       result.d.z = positions.End.y;
        } else {
            result.a.x = positions.End.x;       result.a.z = positions.End.y;
            result.b.x = positions.CtrlEnd.x;   result.b.z = positions.CtrlEnd.y;
            result.c.x = positions.CtrlStart.x; result.c.z = positions.CtrlStart.y;
            result.d.x = positions.Start.x;     result.d.z = positions.Start.y;
        }

        return result;
    }
}
```

---

## Job Refactoring

### Rename and Extend `SlopeTransformJob`

**Modify: `Systems/Slope/NT_SlopeToolSystem.Jobs.cs`**

The job becomes `PathTransformJob` and handles both transforms:

```csharp
private struct PathTransformJob : IJob {
    // Existing fields...
    [ReadOnly] public required TransformConfig Config;  // Replaces SlopeCurveConfig
    [ReadOnly] public required TransformOutputMode OutputMode;  // Replaces SlopeOutputMode
    
    public void Execute() {
        // ... existing node position gathering ...
        
        var startNodeXZ = new float2(startNodeInfo.m_Position.x, startNodeInfo.m_Position.z);
        var endNodeXZ   = new float2(endNodeInfo.m_Position.x, endNodeInfo.m_Position.z);
        
        // === Phase 1: Gather edge metadata (same as before, but also populate shape data) ===
        
        // === Phase 2: Calculate transforms and build computed edges ===
        for (var i = 0; i < edgeCount; i++) {
            // ... existing curve/edge lookup ...
            
            var adjustedBezier = curve.m_Bezier;
            
            // Shape transform first (XZ)
            if (Config.HasShapeTransform) {
                var positions = Config.Shape.Template switch {
                    ShapeTemplate.Straighten => ShapeCalculator.CalculateStraightenedPositions(
                        cumulativeDistance, data.Length, data.CtrlStartRatio, data.CtrlEndRatio,
                        totalLength, startNodeXZ, endNodeXZ),
                    _ => default,
                };
                adjustedBezier = ShapeCalculator.ApplyPositionsToBezier(adjustedBezier, positions, data.IsForward);
            }
            
            // Slope transform second (Y) - operates on potentially shape-modified bezier
            if (Config.HasSlopeTransform) {
                var heights = SlopeCalculator.CalculateEdgeHeights(
                    cumulativeDistance, data.Length, data.CtrlStartRatio, data.CtrlEndRatio,
                    totalLength, startHeight, deltaHeight, Config.Slope);
                adjustedBezier = SlopeCalculator.ApplyHeightsToBezier(adjustedBezier, heights, data.IsForward);
            }
            
            // ... rest of computed edge creation ...
        }
        
        // === Output Phase (unchanged) ===
    }
}
```

---

## File Changes Summary

### New Files
| File | Description |
|------|-------------|
| `Systems/Slope/ShapeCalculator.cs` | `ShapeTemplate`, `ShapeCurveConfig`, `EdgeShapeData`, `EdgePositions`, `ComputedEdgeShape`, `ShapeCalculator` |
| `Systems/Slope/TransformConfig.cs` | `TransformOutputMode`, `TransformFlags`, `TransformConfig` |

### Modified Files
| File | Changes |
|------|---------|
| `Systems/Slope/SlopeCurveConfig.cs` | Add `Preserve = 0` to `SlopeTemplate` enum |
| `Systems/Slope/SlopeCalculator.cs` | Remove `SlopeOutputMode` (moved to `TransformConfig.cs`) |
| `Systems/Slope/NT_SlopeToolSystem.cs` | Update `OperationState.Config` to `TransformConfig`, update `SetTransformationConfig` |
| `Systems/Slope/NT_SlopeToolSystem.Jobs.cs` | Rename job to `PathTransformJob`, add shape transform phase |
| `UI/src/components/toolActionPanel/slope.tsx` | Merge tabs, add shape controls |

---

## Implementation Steps

### Phase 1: Core Data Structures (No Breaking Changes)
1. Create `Systems/Slope/ShapeCalculator.cs` with all shape-related types
2. Create `Systems/Slope/TransformConfig.cs` with `TransformConfig` and `TransformOutputMode`
3. **Do not modify `SlopeTemplate` yet** - keep existing values working

### Phase 2: Parallel Integration
1. Update `OperationState` to use `TransformConfig` (with backward-compatible factory)
2. Update `SetTransformationConfig` to accept `TransformConfig`
3. Update job to accept `TransformConfig` but only use `.Slope` initially (no behavior change)

### Phase 3: Shape Transform Implementation
1. Implement `ShapeCalculator.CalculateStraightenedPositions`
2. Implement `ShapeCalculator.ApplyPositionsToBezier`
3. Add shape transform phase to job (before slope transform)
4. Add intersection handling for shape changes (similar to slope intersection handling)

### Phase 4: UI Updates
1. Add Shape template selector to UI (default: Preserve)
2. Wire up bindings to pass full `TransformConfig`
3. Test both transforms independently and together

### Phase 5: Polish & Additional Templates
1. Add `Preserve` to `SlopeTemplate` (breaking change, do last)
2. Implement `ShapeTemplate.Smooth` (bezier fitting)
3. Implement `ShapeTemplate.EqualSpacing` (redistribute nodes)
4. Add template-specific UI controls

---

## Intersection Handling for Shape Transforms

Similar to how slope transforms adjust connected edges at intersections, shape transforms need equivalent logic:

```csharp
private void HandleShapeIntersections(
    NativeArray<EdgeShapeData> edgeData,
    NativeHashSet<Entity> pathEdgeSet,
    float totalLength,
    float2 pathStartXZ,
    float2 pathEndXZ) {
    // For each intersection node in the path:
    // 1. Calculate the XZ delta (newXZ - oldXZ)
    // 2. For each connected edge NOT in the path:
    //    - If edge starts at intersection: adjust .a and .b by delta
    //    - If edge ends at intersection: adjust .d and .c by delta
    // This preserves the original shape of connected roads
}
```

---

## Future Extensibility

| Feature | Implementation |
|---------|----------------|
| Maintain visual length | Add `TransformFlags.MaintainVisualLength`, recalculate XZ after slope |
| 3D redistribution | Add `ShapeTemplate.Redistribute3D` that considers Y values |
| Undo/redo | Store `TransformConfig` in undo stack |
| Presets | Save/load `TransformConfig` as named presets |

---

## Notes

- The `Preserve` template means "keep existing data" - this is the default for both shape and slope
- **Order matters**: Shape is applied first, then Slope. This allows slope calculations to work on the modified XZ positions
- The existing `ComputedEdgeSlope` can be reused since it stores the final `AdjustedBezier` regardless of which transforms were applied
- All existing slope functionality remains intact until Phase 5
- Consider renaming the `Slope` folder to `Transform` or `PathEdit` in a future cleanup
