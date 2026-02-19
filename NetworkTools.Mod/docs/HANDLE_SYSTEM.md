# Handle System Documentation

This document provides an overview of the Handle system as implemented in `NT_NodeControlToolSystem`. 

---

## Overview

The current Handle system allows users to visually manipulate bezier curve control points on network edges. Handles are lightweight ECS entities that:
- Represent bezier control points (a, b, c, d)
- Can be hovered and highlighted
- Can be dragged to modify curve geometry
- Apply changes back to the actual `Curve` component on edges

---

## Components

### `NT_Handle`
**File:** `Components/NT_Handle.cs`

Tag component with type flags indicating what kind of handle this is.

### `NT_HandleLink`
**File:** `Components/NT_HandleLink.cs`

Links the handle to the entities it represents/controls.

### `NT_HandlePosition`
**File:** `Components/NT_HandlePosition.cs`

Stores the handle's world position (can differ from the actual bezier point during drag).

### Supporting Components
- `NT_Highlighted` - Added when hovering over a handle
- `NT_Selected` - Added when actively dragging a handle

---

## Lifecycle

### Creation
**Location:** `NT_NodeControlToolSystem.CreateHandles()`

Handles are created when a node is selected:

```csharp
private void CreateHandles(Entity node) {
    var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);

    for (var i = 0; i < connectedEdges.Length; i++) {
        var edgeEntity = connectedEdges[i].m_Edge;
        var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
        var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
        var isForward = edge.m_Start == node;

        // Endpoint handle (a or d)
        var endpointFlags = HandleTypeFlags.BezierPoint |
            (isForward ? HandleTypeFlags.BezierStartPoint : HandleTypeFlags.BezierEndPoint);
        m_Handles.Add(CreateHandle(node, edgeEntity, isForward ? 0 : 3, 
            isForward ? curve.m_Bezier.a : curve.m_Bezier.d, endpointFlags));

        // Control point handle (b or c)
        var controlFlags = HandleTypeFlags.BezierPoint | HandleTypeFlags.BezierControlPoint;
        m_Handles.Add(CreateHandle(node, edgeEntity, isForward ? 1 : 2, 
            isForward ? curve.m_Bezier.b : curve.m_Bezier.c, controlFlags));
    }
}
```

**Key mapping based on `isForward` (edge.m_Start == node):**

| isForward | Endpoint Key | Endpoint Position | Control Key | Control Position |
|-----------|--------------|-------------------|-------------|------------------|
| true      | 0 (a)        | curve.m_Bezier.a  | 1 (b)       | curve.m_Bezier.b |
| false     | 3 (d)        | curve.m_Bezier.d  | 2 (c)       | curve.m_Bezier.c |

### Storage
Handles are stored in a `NativeList<Entity> m_Handles` for iteration during raycasting and cleanup.

### Destruction
**Location:** `NT_NodeControlToolSystem.DestroyHandles()`

```csharp
private void DestroyHandles() {
    for (var i = 0; i < m_Handles.Length; i++) {
        var handle = m_Handles[i];
        if (EntityManager.Exists(handle)) {
            EntityManager.DestroyEntity(handle);
        }
    }
    m_Handles.Clear();
}
```

Called when:
- Transitioning back to `NoSelection` state
- Tool stops running (`OnStopRunning`)
- Tool is destroyed (`OnDestroy`)

---

## Interaction

### Ray-Sphere Intersection (Raycasting)
**Location:** `NT_NodeControlToolSystem.GetClosestHandleFromRay()` + `TryRaySphereIntersection()`

Since handles don't have physical colliders, we perform manual ray-sphere intersection:

```csharp
private Entity GetClosestHandleFromRay(float handleRadius) {
    var camera = Camera.main;
    var mousePos = Mouse.current.position.ReadValue();
    var ray = camera.ScreenPointToRay(mousePos);
    var rayOrigin = (float3)ray.origin;
    var rayDir = math.normalize((float3)ray.direction);

    var closestHandle = Entity.Null;
    var closestT = float.MaxValue;

    for (var i = 0; i < m_Handles.Length; i++) {
        var handleEntity = m_Handles[i];
        var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;

        if (TryRaySphereIntersection(rayOrigin, rayDir, handlePos, handleRadius, out var t)) {
            if (t < closestT) {
                closestT = t;
                closestHandle = handleEntity;
            }
        }
    }
    return closestHandle;
}
```

**Constants:**
- `HandleHitRadius = 2f` - Sphere radius for hit detection

### Input State Machine
**Location:** `NT_NodeControlToolSystem.OnUpdate()`

```
┌─────────────────────────────────────────────────────────────┐
│                    InputInteractionState                     │
├─────────────┬─────────────────┬─────────────────────────────┤
│    Idle     │  PendingAction  │         Dragging            │
├─────────────┼─────────────────┼─────────────────────────────┤
│ - Hover     │ - Waiting for   │ - UpdateHandleDragPosition  │
│ - Highlight │   drag/click    │ - ApplyHandlePositionToCurve│
│ - MouseDown │   threshold     │ - NT_Selected on handle     │
│   → Pending │ - Click → Idle  │ - MouseUp → Idle            │
│             │ - Drag → Drag   │                             │
└─────────────┴─────────────────┴─────────────────────────────┘
```

**Drag threshold:** `DragThreshold = 0.5f` world units

### XZ Plane Projection (Dragging)
**Location:** `NT_NodeControlToolSystem.TryGetXZPlaneIntersection()`

During drag, we project the mouse onto a horizontal plane at the handle's current Y position:

```csharp
private bool TryGetXZPlaneIntersection(float planeY, out float3 intersection) {
    // Ray from camera through mouse
    var ray = camera.ScreenPointToRay(mousePos);
    
    // Solve: origin.y + t * direction.y = planeY
    var t = (planeY - rayOrigin.y) / rayDirection.y;
    
    intersection = rayOrigin + t * rayDirection;
    return true;
}
```

This keeps the handle at its original elevation while allowing XZ movement.

---

## Applying Changes

### Updating Bezier Curves
**Location:** `NT_NodeControlToolSystem.ApplyHandlePositionToCurve()`

```csharp
private void ApplyHandlePositionToCurve(Entity handleEntity) {
    var handleLink = EntityManager.GetComponentData<NT_HandleLink>(handleEntity);
    var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
    var edgeEntity = handleLink.LinkedEdge;
    var key = handleLink.Key;

    var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
    var bezier = curve.m_Bezier;

    switch (key) {
        case 0: bezier.a = handlePos; break;
        case 1: bezier.b = handlePos; break;
        case 2: bezier.c = handlePos; break;
        case 3: bezier.d = handlePos; break;
    }

    curve.m_Bezier = bezier;
    EntityManager.SetComponentData(edgeEntity, curve);

    // Signal game to recalculate
    EntityManager.AddComponent<Updated>(edgeEntity);
}
```

**Called during:** Every frame while dragging (live preview)

---

## File References

| File | Purpose |
|------|---------|
| `Components/NT_Handle.cs` | Handle tag + type flags |
| `Components/NT_HandleLink.cs` | Links handle to node/edge |
| `Components/NT_HandlePosition.cs` | Handle world position |
| `Systems/Tools/NodeControl/NT_NodeControlToolSystem.cs` | Main tool logic |
| `Systems/Tools/NodeControl/NT_NodeControlToolSystem.Lifecycle.cs` | OnCreate, OnDestroy, etc. |
| `Systems/Rendering/NT_OverlayRenderSystem.DrawHandlesJob.cs` | Handle rendering |

---

## Related Documents

- [HANDLE_SYSTEM_ARCHITECTURE.md](HANDLE_SYSTEM_ARCHITECTURE.md) - Reusable handle system architecture
- [HANDLE_SYSTEM_FUTURE.md](HANDLE_SYSTEM_FUTURE.md) - Future considerations (undo/redo, constraints, etc.)
