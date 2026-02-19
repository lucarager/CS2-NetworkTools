# Handle System Architecture

This document describes the reusable handle system architecture for NetworkTools. Handles are lightweight ECS entities that provide in-world visual controls for manipulating various aspects of roads and network elements.

---

## Overview

The handle system is designed to be:
- **Reusable**: Any tool system can create and manage handles
- **Extensible**: Support for position, parameter, rotation, and scale handles
- **Constrained**: Optional movement constraints for intuitive manipulation
- **ECS-native**: Uses component composition rather than inheritance

### Design Principles

1. **Centralized Management**: Handle lifecycle is managed in `NT_BaseToolSystem`
2. **Virtual Hooks**: Tools customize behavior by overriding hook methods
3. **Component Composition**: Handle types defined by component combinations
4. **Single Active Tool**: Only one tool can be active, simplifying handle ownership

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         NT_BaseToolSystem                                │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    Handle Management (Partial)                   │    │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │    │
│  │  │ m_Handles       │  │ HandleInputState│  │ m_DraggedHandle │  │    │
│  │  │ NativeList      │  │ Idle/Pending/   │  │ Entity          │  │    │
│  │  │                 │  │ Dragging        │  │                 │  │    │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘  │    │
│  │                                                                  │    │
│  │  Methods:                                                        │    │
│  │  • CreatePositionHandle()    • DestroyAllHandles()              │    │
│  │  • CreateParameterHandle()   • DestroyHandlesWithFlags()        │    │
│  │  • CreateLineHandle()        • UpdateHandleDragPosition()       │    │
│  │  • CreateCircleHandle()      • GetClosestHandleFromRay()        │    │
│  │                                                                  │    │
│  │  Virtual Hooks:                                                  │    │
│  │  • OnHandleDragStart()       • OnHandleDragEnd()                │    │
│  │  • OnHandleDragging()        • OnHandleClick()                  │    │
│  └─────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
┌─────────────────────────────────┐   ┌─────────────────────────────────┐
│   NT_NodeControlToolSystem      │   │   NT_PathTransformToolSystem    │
│                                 │   │                                 │
│ Overrides:                      │   │ Overrides:                      │
│ • OnHandleDragging()            │   │ • OnHandleDragging()            │
│   → ApplyHandlePositionToCurve  │   │   → ApplyHandleToConfig         │
│ • OnHandleDragEnd()             │   │ • OnHandleDragEnd()             │
│   → Finalize curve changes      │   │   → Finalize config changes     │
│                                 │   │                                 │
│ Creates:                        │   │ Creates:                        │
│ • Bezier control point handles  │   │ • Shape control handles         │
│                                 │   │ • Slope control handles         │
│                                 │   │ • Parameter value handles       │
└─────────────────────────────────┘   └─────────────────────────────────┘
```

---

## Components

### Core Handle Components

| Component | File | Purpose |
|-----------|------|---------|
| `NT_Handle` | `Components/Handles/NT_Handle.cs` | Tag + type flags identifying handle purpose |
| `NT_HandleLink` | `Components/Handles/NT_HandleLink.cs` | Links handle to controlled entities |
| `NT_HandlePosition` | `Components/Handles/NT_HandlePosition.cs` | World position of the handle |
| `NT_HandleValue` | `Components/Handles/NT_HandleValue.cs` | Scalar value for parameter handles |
| `NT_HandleConstraints` | `Components/Handles/NT_HandleConstraints.cs` | Optional movement constraints |
| `NT_HandleLine` | `Components/Handles/NT_HandleLine.cs` | Two-point line segment data |
| `NT_HandleCircle` | `Components/Handles/NT_HandleCircle.cs` | Circle/arc with center, radius, normal |

### Geometric Handle Components

```csharp
/// <summary>
/// Data for a line handle representing two connected points.
/// </summary>
public struct NT_HandleLine : IComponentData {
    /// <summary>First endpoint of the line segment.</summary>
    public float3 PointA;

    /// <summary>Second endpoint of the line segment.</summary>
    public float3 PointB;
}

/// <summary>
/// Data for a circle handle representing a radius/dimension.
/// </summary>
public struct NT_HandleCircle : IComponentData {
    /// <summary>Center point of the circle.</summary>
    public float3 Center;

    /// <summary>Radius of the circle.</summary>
    public float Radius;

    /// <summary>Normal vector defining the plane the circle lies on.</summary>
    public float3 Normal;
}
```

### Supporting Components

| Component | Purpose |
|-----------|---------|
| `NT_Highlighted` | Added when hovering over a handle |
| `NT_Selected` | Added when actively dragging a handle |

---

## Handle Types

Handles are defined by their component composition:

### Position Handle (Bezier Control Points)

```csharp
// Components: NT_Handle + NT_HandlePosition + NT_HandleLink
var handle = CreatePositionHandle(
    linkedEntity: nodeEntity,
    linkedEdge: edgeEntity,
    key: 1,  // bezier point b
    position: curve.m_Bezier.b,
    typeFlags: HandleTypeFlags.BezierPoint | HandleTypeFlags.BezierControlPoint
);
```

### Parameter Handle (Config Values)

```csharp
// Components: NT_Handle + NT_HandlePosition + NT_HandleLink + NT_HandleValue
var handle = CreateParameterHandle(
    linkedEntity: configEntity,
    key: ConfigKeys.SmoothingFactor,
    position: worldPosition,
    value: 0.5f,
    minValue: 0f,
    maxValue: 1f,
    typeFlags: HandleTypeFlags.ShapeControl
);
```

### Constrained Handle

```csharp
// Add NT_HandleConstraints for movement restrictions
var handle = CreatePositionHandle(
    linkedEntity: entity,
    linkedEdge: Entity.Null,
    key: 0,
    position: startPos,
    typeFlags: HandleTypeFlags.Position | HandleTypeFlags.SlopeControl,
    constraints: NT_HandleConstraints.AxisOnly(new float3(0, 1, 0))  // Y-axis only
);
```

### Line Handle (Two Connected Points)

Line handles represent two points that can be dragged together. Useful for controlling segments or paired control points.

```csharp
// Components: NT_Handle + NT_HandlePosition + NT_HandleLink + NT_HandleLine
var handle = CreateLineHandle(
    linkedEntity: edgeEntity,
    key: 0,
    pointA: curve.m_Bezier.a,
    pointB: curve.m_Bezier.b,
    typeFlags: HandleTypeFlags.Line | HandleTypeFlags.ShapeControl
);
```

**Drag Behavior:** Moving the handle translates both points together, maintaining their relative positions.

### Circle Handle (Radius/Dimension Control)

Circle handles represent a dimension via radius. Dragging changes the radius based on distance from center.

```csharp
// Components: NT_Handle + NT_HandlePosition + NT_HandleLink + NT_HandleCircle
var handle = CreateCircleHandle(
    linkedEntity: configEntity,
    key: ConfigKeys.InfluenceRadius,
    center: nodePosition,
    radius: 10f,
    normal: new float3(0, 1, 0),  // Horizontal circle
    typeFlags: HandleTypeFlags.Circle | HandleTypeFlags.ShapeControl
);
```

**Drag Behavior:** Moving outward/inward from center increases/decreases the radius value.

---

## HandleTypeFlags

```csharp
[Flags]
public enum HandleTypeFlags {
    None = 0,

    // === Bezier Handle Types ===
    BezierPoint        = 1 << 0,   // Represents a bezier curve point
    BezierStartPoint   = 1 << 1,   // Start point (a)
    BezierEndPoint     = 1 << 2,   // End point (d)
    BezierControlPoint = 1 << 3,   // Control point (b or c)

    // === Transform Handle Types ===
    Position           = 1 << 4,   // Controls world position
    Parameter          = 1 << 5,   // Controls scalar parameter
    Rotation           = 1 << 6,   // Controls rotation (future)
    Scale              = 1 << 7,   // Controls scale (future)

    // === Purpose Categories ===
    ShapeControl       = 1 << 8,   // Horizontal curve manipulation
    SlopeControl       = 1 << 9,   // Vertical elevation manipulation

    // === Visual Hints ===
    Primary            = 1 << 10,  // Larger/prominent display
    Secondary          = 1 << 11,  // Smaller/subdued display
    
    // === Geometric Handle Types ===
    Line               = 1 << 12,  // Two-point line segment
    Circle             = 1 << 13,  // Circle/arc for radius control
}
```

---

## Constraint System

The `NT_HandleConstraints` component restricts handle movement:

```csharp
[Flags]
public enum ConstraintFlags {
    None           = 0,
    LockX          = 1 << 0,  // Lock X axis
    LockY          = 1 << 1,  // Lock Y axis (XZ plane movement)
    LockZ          = 1 << 2,  // Lock Z axis
    ClampToBounds  = 1 << 3,  // Clamp to min/max bounds
    SnapToAxis     = 1 << 4,  // Snap to specified axis direction
}
```

### Common Constraint Patterns

| Pattern | Usage |
|---------|-------|
| `LockY` | Default for most handles (XZ plane movement) |
| `SnapToAxis(0,1,0)` | Vertical-only movement for elevation handles |
| `ClampToBounds` | Keep handle within a defined region |

---

## Input State Machine

```
┌─────────────────────────────────────────────────────────────────┐
│                      HandleInputState                            │
├─────────────┬─────────────────┬─────────────────────────────────┤
│    Idle     │  PendingAction  │           Dragging              │
├─────────────┼─────────────────┼─────────────────────────────────┤
│ • Hover     │ • Mouse down    │ • UpdateHandleDragPosition()    │
│ • Highlight │ • Wait for      │ • OnHandleDragging() called     │
│ • MouseDown │   threshold     │ • NT_Selected on handle         │
│   → Pending │ • Click → Idle  │ • MouseUp → OnHandleDragEnd()   │
│             │ • Drag → Drag   │            → Idle               │
└─────────────┴─────────────────┴─────────────────────────────────┘

Drag Threshold: 0.5 world units
```

---

## Lifecycle

### Handle Creation

Handles are created by tools when entering a state that requires them:

```csharp
// In NT_NodeControlToolSystem
private void SelectNode(Entity entity) {
    // ... selection logic ...
    
    // Create handles for all connected edges
    CreateHandles(entity);
}

private void CreateHandles(Entity node) {
    var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);
    
    for (var i = 0; i < connectedEdges.Length; i++) {
        var edgeEntity = connectedEdges[i].m_Edge;
        var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
        
        // Create endpoint and control point handles
        CreatePositionHandle(node, edgeEntity, 0, curve.m_Bezier.a, 
            HandleTypeFlags.BezierPoint | HandleTypeFlags.BezierStartPoint);
        CreatePositionHandle(node, edgeEntity, 1, curve.m_Bezier.b,
            HandleTypeFlags.BezierPoint | HandleTypeFlags.BezierControlPoint);
    }
}
```

### Handle Destruction

Handles are destroyed when:
- Tool transitions to a state that doesn't need them
- Tool stops running (`OnStopRunning`)
- Tool is destroyed (`OnDestroy`)

```csharp
// Destroy all handles
DestroyAllHandles();

// Or destroy specific types
DestroyHandlesWithFlags(HandleTypeFlags.ShapeControl);
```

---

## Implementing Handle Support in a Tool

### Step 1: Override Virtual Hooks

```csharp
public partial class MyToolSystem : NT_BaseToolSystem {
    
    protected override void OnHandleDragStart(Entity handle) {
        // Store initial state for undo/redo
        var link = GetHandleLink(handle);
        m_InitialState = CaptureState(link.LinkedEntity);
    }
    
    protected override void OnHandleDragging(Entity handle) {
        // Apply live preview
        ApplyHandleChanges(handle);
    }
    
    protected override void OnHandleDragEnd(Entity handle) {
        // Finalize changes
        CommitChanges(handle);
    }
}
```

### Step 2: Create Handles at Appropriate Times

```csharp
private void EnterEditMode(Entity target) {
    // Create handles for the target
    var positions = GetControlPositions(target);
    
    for (var i = 0; i < positions.Length; i++) {
        CreatePositionHandle(
            target, Entity.Null, i, positions[i],
            HandleTypeFlags.Position | HandleTypeFlags.Primary
        );
    }
}

private void ExitEditMode() {
    DestroyAllHandles();
}
```

### Step 3: Override ShouldRaycastHandles

```csharp
protected override bool ShouldRaycastHandles => 
    CurrentState == MyToolState.Editing && m_Handles.Length > 0;
```

---

## File Reference

| File | Purpose |
|------|---------|
| `Components/Handles/NT_Handle.cs` | Handle tag + type flags |
| `Components/Handles/NT_HandleLink.cs` | Links handle to controlled entities |
| `Components/Handles/NT_HandlePosition.cs` | Handle world position |
| `Components/Handles/NT_HandleValue.cs` | Scalar value for parameter handles |
| `Components/Handles/NT_HandleConstraints.cs` | Movement constraints |
| `Components/Handles/NT_HandleLine.cs` | Line segment (two points) |
| `Components/Handles/NT_HandleCircle.cs` | Circle/arc geometry |
| `Systems/Tools/NT_BaseToolSystem.Handles.cs` | Handle management (partial class) |
| `Systems/Rendering/NT_OverlayRenderSystem.DrawHandlesJob.cs` | Handle rendering |

---

## Raycasting

The `GetClosestHandleFromRay()` method performs type-aware intersection testing:

```csharp
protected Entity GetClosestHandleFromRay(float handleRadius = HandleHitRadius) {
    // ... ray setup ...

    for (var i = 0; i < m_Handles.Length; i++) {
        var handleEntity = m_Handles[i];
        var handleData = EntityManager.GetComponentData<NT_Handle>(handleEntity);

        // Type-aware intersection
        if (handleData.HasAnyFlag(HandleTypeFlags.Line)) {
            var line = EntityManager.GetComponentData<NT_HandleLine>(handleEntity);
            if (TryRayLineIntersection(rayOrigin, rayDir, line.PointA, line.PointB, threshold, out t)) { ... }
        }
        else if (handleData.HasAnyFlag(HandleTypeFlags.Circle)) {
            var circle = EntityManager.GetComponentData<NT_HandleCircle>(handleEntity);
            if (TryRayCircleIntersection(rayOrigin, rayDir, circle.Center, circle.Radius, circle.Normal, out t)) { ... }
        }
        else {
            // Default: point/sphere intersection
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            if (TryRaySphereIntersection(rayOrigin, rayDir, handlePos, handleRadius, out t)) { ... }
        }
    }

    return closestHandle;
}
```

### Intersection Methods

| Method | Handle Type | Description |
|--------|-------------|-------------|
| `TryRaySphereIntersection` | Position, Parameter | Point handles with radius |
| `TryRayLineIntersection` | Line | Closest point on line segment |
| `TryRayCircleIntersection` | Circle | Intersection with circle arc |

---

## Future Considerations

### Rotation Handles
- Add `NT_HandleRotation` component with `quaternion` value
- Implement arc-based drag interaction
- Consider gimbal lock prevention

### Scale Handles
- Add `NT_HandleScale` component with `float3` value
- Implement axis-aligned scaling
- Support uniform vs non-uniform scaling

### Undo/Redo Support
- Capture state in `OnHandleDragStart()`
- Create command objects for change history
- Integrate with game's undo system

### Multi-Handle Selection
- Track selected handles in a `NativeHashSet`
- Apply transformations to all selected handles
- Consider relative vs absolute positioning

---

## Related Documents

- [HANDLE_SYSTEM.md](HANDLE_SYSTEM.md) - Original handle implementation details
- [HANDLE_SYSTEM_FUTURE.md](HANDLE_SYSTEM_FUTURE.md) - Extended future considerations
