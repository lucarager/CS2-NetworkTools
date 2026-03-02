# Rendering System Rearchitecture

## Overview

This document outlines the refactoring of NetworkTools' rendering control system from a hybrid boolean-flag/ECS approach to a primarily ECS-driven architecture with clear separation of concerns.

---

## Problem Statement

### Current State

The rendering system uses two parallel mechanisms:

| Mechanism | Location | Purpose |
|-----------|----------|---------|
| Boolean flags | `NT_BaseToolSystem` | Gate whether rendering jobs run |
| ECS components | Entities | Control *how* individual entities render |

**Current flags on `NT_BaseToolSystem`:**
```csharp
public bool RenderEligibleEdges = false;
public bool RenderEligibleNodes = false;
public bool RenderHandles = false;
public bool RenderSlopeTooltips = false;
public bool RenderTempEdges = false;
public bool RenderTempNodes = false;
```

### Issues

1. **Redundancy**: Flags like `RenderEligibleNodes` gate jobs, but ECS queries already filter entities. An empty query = no work.
2. **Inconsistency**: Some rendering is controlled by flags, some by ECS components, with no clear rule.
3. **Scattered data**: Tooltip system recalculates slopes instead of reading computed data, causing imprecision.
4. **Scalability**: Adding new render features requires new booleans.

---

## Architectural Decisions

### Decision 1: ECS-Driven Overlay Rendering

**Remove boolean flags for overlays.** Let ECS query emptiness determine whether jobs run.

```csharp
// Before
if (tool.RenderEligibleNodes) {
    ScheduleDrawNodesJob();
}

// After
if (!m_NodeQuery.IsEmptyIgnoreFilter) {
    ScheduleDrawNodesJob();
}
```

**Exception**: Keep flags for **preview opt-in** (`RenderTempEdges`, `RenderTempNodes`) since tools may not always want Temp entity rendering.

### Decision 2: Slope Data Stored in ECS

**Move slope calculation to tools, store in ECS.** Tooltip system reads data instead of recalculating.

Due to game pipeline constraints, Temp entities cannot have components added directly. Solution: store both current and preview slopes on the **original** edge entity.

```csharp
public struct NT_SlopeData : IComponentData {
    public float CurrentSlopePercent;
    public float PreviewSlopePercent;
    public bool HasPreview;
}
```

Tooltip system:
- Original edges → read `CurrentSlopePercent`
- Temp edges → lookup `Temp.m_Original` → read `PreviewSlopePercent`

### Decision 3: Config Intent Flags (Not Render Flags)

**Configs control feature intent, not rendering directly.**

`ShapeTransformConfig.ShowSlopeTooltips` is an *intent flag* that tells the tool whether to add `NT_SlopeData` components. The presence of the component is what triggers rendering.

```csharp
// In tool when selecting edges:
if (m_ActiveConfig.ShowSlopeTooltips) {
    EntityManager.AddComponentData(edgeEntity, new NT_SlopeData { ... });
}
```

### Decision 4: Clear Layer Responsibilities

| Layer | Responsibility | Examples |
|-------|----------------|----------|
| **ECS Components** | What entities render + their data | `NT_Highlighted`, `NT_Selected`, `NT_SlopeData` |
| **ECS Queries** | Whether jobs run (via `IsEmptyIgnoreFilter`) | `m_NodeQuery`, `m_EdgeQuery` |
| **Config Intent** | Which features are active for an operation | `ShowSlopeTooltips` |
| **Tool Flags** | Preview opt-in only | `RenderTempEdges`, `RenderTempNodes` |

---

## Component Lifecycle

### `NT_SlopeData` Lifecycle

| Event | Action | Responsible System |
|-------|--------|--------------------|
| Edge selected | Add `NT_SlopeData` (if `ShowSlopeTooltips` enabled) | Tool system |
| Preview created | Set `HasPreview = true`, update `PreviewSlopePercent` | Tool system |
| Preview cancelled | Set `HasPreview = false` | Tool system |
| Transformation applied | Remove `NT_SlopeData` (cleanup all) | `NT_BaseToolSystem.CleanupSlopeData()` |
| Selection cleared | Remove `NT_SlopeData` (cleanup all) | `NT_BaseToolSystem.CleanupSlopeData()` |
| Tool deactivated | Remove `NT_SlopeData` (cleanup all) | `NT_BaseToolSystem.OnStopRunning()` |
| Config changes mid-operation | Add/remove based on new config | Tool system |

**Key invariant**: `NT_SlopeData` exists on an edge **if and only if** that edge is selected AND `ShowSlopeTooltips` is enabled.

### Preview State Synchronization

To prevent stale `HasPreview` state:

```csharp
// In tool system when preview is cancelled/cleared:
private void ClearPreviewState() {
    var edges = m_EdgesWithSlopeDataQuery.ToEntityArray(Allocator.Temp);
    foreach (var edge in edges) {
        var slopeData = EntityManager.GetComponentData<NT_SlopeData>(edge);
        slopeData.HasPreview = false;
        EntityManager.SetComponentData(edge, slopeData);
    }
    edges.Dispose();
}
```

Call `ClearPreviewState()` when:
- User cancels preview (e.g., right-click)
- Temp entities are destroyed
- Before starting a new preview calculation

---

## Integration Strategy: Leveraging Existing Data Pipeline

Rather than scattering slope management across many integration points, we can **extend the existing data pipeline** that `NT_RoadShapeToolSystem` already uses.

### Current Data Flow

```
Selection Changes
       ↓
RefreshPathData()  →  GatherPathDataJob  →  m_EdgeStates (NativeList<EdgeState>)
       ↓                                            ↓
InitializeCurrentTransform()                        ↓
       ↓                                            ↓
OnUpdate()  →  SchedulePathTransformJob  →  Transforms EdgeStates → Creates Temp entities
       ↓
Tooltip System reads from entities
```

### Proposed Enhancement

**Extend `EdgeState` to carry slope data through the pipeline:**

```csharp
// In EdgeState.cs - add these fields:
public struct EdgeState {
    // ... existing fields ...

    // === Slope Data (computed by jobs) ===

    /// <summary>
    /// Current slope percentage (before transformation).
    /// Computed by GatherPathDataJob.
    /// </summary>
    public float CurrentSlopePercent;

    /// <summary>
    /// Preview slope percentage (after transformation).
    /// Computed by ShapeTransformJob.
    /// </summary>
    public float PreviewSlopePercent;
}
```

### Updated Data Flow

```
Selection Changes
       ↓
RefreshPathData()  →  GatherPathDataJob  →  m_EdgeStates 
       ↓                    ↓                   (includes CurrentSlopePercent)
       ↓              Computes slopes           ↓
       ↓                                        ↓
SyncSlopeDataToEntities()  ←──────────────────────┘
       ↓                         (writes NT_SlopeData.CurrentSlopePercent)
       ↓
OnUpdate()  →  ShapeTransformJob  →  Transforms EdgeStates
       ↓              ↓                (computes PreviewSlopePercent)
       ↓              ↓
SyncPreviewSlopesToEntities()  ←─────────┘
       ↓                         (writes NT_SlopeData.PreviewSlopePercent + HasPreview)
       ↓
Tooltip System reads NT_SlopeData from entities
```

### Implementation

#### 1. Extend `GatherPathDataJob` to compute current slopes

```csharp
// In GatherPathDataJob:
for (int i = 0; i < edges.Length; i++) {
    var edgeState = edges[i];

    // ... existing bezier/length calculations ...

    // Compute current slope (path-direction aware)
    float pathStartY = edgeState.IsForward ? edgeState.Bezier.a.y : edgeState.Bezier.d.y;
    float pathEndY = edgeState.IsForward ? edgeState.Bezier.d.y : edgeState.Bezier.a.y;
    edgeState.CurrentSlopePercent = (pathEndY - pathStartY) / edgeState.Length * 100f;

    edges[i] = edgeState;
}
```

#### 2. Single sync point after `RefreshPathData()`

```csharp
private void RefreshPathData() {
    // ... existing GatherPathDataJob code ...

    m_PathDataValid = true;

    // NEW: Sync slope data to entities (single integration point!)
    if (ShapeTransformConfig.ShowSlopeTooltips) {
        SyncSlopeDataToEntities();
    }

    InitializeCurrentTransform();
}

/// <summary>
/// Writes slope data from EdgeStates to NT_SlopeData components.
/// Single sync point - called only from RefreshPathData().
/// </summary>
private void SyncSlopeDataToEntities() {
    foreach (var edgeState in m_EdgeStates) {
        var slopeData = new NT_SlopeData {
            CurrentSlopePercent = edgeState.CurrentSlopePercent,
            PreviewSlopePercent = 0f,
            HasPreview = false
        };

        if (EntityManager.HasComponent<NT_SlopeData>(edgeState.EdgeEntity)) {
            EntityManager.SetComponentData(edgeState.EdgeEntity, slopeData);
        } else {
            EntityManager.AddComponentData(edgeState.EdgeEntity, slopeData);
        }
    }
}
```

#### 3. Extend `ShapeTransformJob` to output preview slopes

```csharp
// In ShapeTransformJob - after transformation:
public NativeArray<float> PreviewSlopes; // Output array

public void Execute() {
    // ... existing transformation code ...

    // After transformation, compute preview slopes
    for (int i = 0; i < edges.Length; i++) {
        var edge = edges[i];
        float pathStartY = edge.IsForward ? edge.Bezier.a.y : edge.Bezier.d.y;
        float pathEndY = edge.IsForward ? edge.Bezier.d.y : edge.Bezier.a.y;
        PreviewSlopes[i] = (pathEndY - pathStartY) / edge.Length * 100f;
    }

    // ... existing output code ...
}
```

#### 4. Single sync point after preview job

```csharp
private JobHandle Update(JobHandle inputDeps) {
    if (!m_UpdateNeeded) {
        applyMode = ApplyMode.None;
        return inputDeps;
    }

    applyMode = ApplyMode.Clear;
    inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
    inputDeps = SchedulePathTransformJob(inputDeps, ToolOutputMode.Preview);

    // NEW: Sync preview slopes after job completes
    if (ShapeTransformConfig.ShowSlopeTooltips) {
        inputDeps.Complete();
        SyncPreviewSlopesToEntities();
    }

    m_UpdateNeeded = false;
    return inputDeps;
}

/// <summary>
/// Updates preview slope data on NT_SlopeData components.
/// Single sync point - called only from Update().
/// </summary>
private void SyncPreviewSlopesToEntities() {
    for (int i = 0; i < m_EdgeStates.Length; i++) {
        var edgeEntity = m_EdgeStates[i].EdgeEntity;
        if (EntityManager.TryGetComponent<NT_SlopeData>(edgeEntity, out var slopeData)) {
            slopeData.PreviewSlopePercent = m_PreviewSlopes[i]; // From job output
            slopeData.HasPreview = true;
            EntityManager.SetComponentData(edgeEntity, slopeData);
        }
    }
}
```

#### 5. Clear preview state in `Clear()`

```csharp
private JobHandle Clear(JobHandle inputDeps) {
    // Clear preview state (single point for preview cancellation)
    if (ShapeTransformConfig.ShowSlopeTooltips) {
        ClearPreviewState();
    }

    applyMode = ApplyMode.Clear;
    inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
    return inputDeps;
}
```

---

### Comparison: Scattered vs. Pipeline-Integrated

| Aspect | Scattered Approach | Pipeline-Integrated |
|--------|-------------------|---------------------|
| **Integration points** | 6+ locations | 3 locations |
| **Slope calculation** | Duplicated in tooltip system | Once in `GatherPathDataJob` |
| **Preview slopes** | Calculated on main thread | Calculated in `ShapeTransformJob` |
| **Sync to entities** | Multiple places | `RefreshPathData()` + `Update()` |
| **Traversal direction** | Must pass context around | Already in `EdgeState.IsForward` |
| **Cleanup** | Spread across methods | Existing `CleanupHighlights()` |

### Key Benefits

1. **Leverages existing job pipeline** - Slopes computed where curves already exist
2. **Single source of truth** - `EdgeState` carries all edge data
3. **Minimal new code paths** - Extends existing methods, doesn't add new ones
4. **Traversal direction baked in** - `EdgeState.IsForward` already solved
5. **Natural cleanup** - Follows existing `RefreshPathData()` / `Clear()` flow

---

## Revised Migration Checklist

### Phase 1: Extend Data Structures
- [ ] Add `CurrentSlopePercent` and `PreviewSlopePercent` fields to `EdgeState`
- [ ] Create `NT_SlopeData.cs` component
- [ ] Add `NativeArray<float> m_PreviewSlopes` field to tool system

### Phase 2: Update Jobs
- [ ] `GatherPathDataJob`: Compute `CurrentSlopePercent` for each edge
- [ ] `ShapeTransformJob`: Output `PreviewSlopes` array after transformation

### Phase 3: Add Sync Points (3 locations only)
- [ ] `RefreshPathData()`: Call `SyncSlopeDataToEntities()` after job
- [ ] `Update()`: Call `SyncPreviewSlopesToEntities()` after preview job
- [ ] `Clear()`: Call `ClearPreviewState()` 

### Phase 4: Update Tooltip System
- [ ] Read from `NT_SlopeData` instead of recalculating
- [ ] Use `TryGetComponent` for defensive lookups

### Phase 5: Cleanup Integration
- [ ] Add `CleanupSlopeData()` to `NT_BaseToolSystem`
- [ ] Call from existing `CleanupHighlights()` or `OnStopRunning()`

---

## Component Changes

### New Components

#### `NT_SlopeData`
```csharp
// NetworkTools.Mod\Components\NT_SlopeData.cs
namespace NetworkTools.Components {
    using Unity.Entities;

    /// <summary>
    /// Slope data for an edge, supporting both current and preview states.
    /// Added to the original edge entity; Temp entities reference back via Temp.m_Original.
    /// </summary>
    public struct NT_SlopeData : IComponentData {
        /// <summary>
        /// Current slope percentage of the edge.
        /// </summary>
        public float CurrentSlopePercent;

        /// <summary>
        /// Preview slope percentage (after transformation).
        /// Only valid when HasPreview is true.
        /// </summary>
        public float PreviewSlopePercent;

        /// <summary>
        /// Whether preview data is available.
        /// </summary>
        public bool HasPreview;
    }
}
```

### Existing Components (No Changes)

- `NT_Highlighted` - Already has `NodeRenderMode`/`EdgeRenderMode` flags ✓
- `NT_Selected`, `NT_Eligible`, `NT_SelectedFirst`, `NT_SelectedLast` - Marker components ✓
- `RenderModes.cs` - `NodeRenderMode`, `EdgeRenderMode` flags ✓

---

## System Changes

### `NT_BaseToolSystem`

#### Remove
```csharp
// DELETE these fields:
public bool RenderEligibleEdges = false;
public bool RenderEligibleNodes = false;
public bool RenderHandles = false;
public bool RenderSlopeTooltips = false;
```

#### Keep
```csharp
// KEEP these (preview opt-in):
public bool RenderTempEdges = false;
public bool RenderTempNodes = false;
```

#### Add
```csharp
// Query for slope data cleanup
protected EntityQuery m_EdgesWithSlopeDataQuery;

// In OnCreate():
m_EdgesWithSlopeDataQuery = SystemAPI.QueryBuilder()
    .WithAll<Edge, NT_SlopeData>()
    .Build();

/// <summary>
/// Removes NT_SlopeData from all edges. Call during cleanup.
/// </summary>
protected void CleanupSlopeData() {
    if (!m_EdgesWithSlopeDataQuery.IsEmptyIgnoreFilter) {
        EntityManager.RemoveComponent<NT_SlopeData>(m_EdgesWithSlopeDataQuery);
    }
}

/// <summary>
/// Clears preview state without removing components.
/// Use when cancelling preview but keeping selection.
/// </summary>
protected void ClearPreviewState() {
    var edges = m_EdgesWithSlopeDataQuery.ToEntityArray(Allocator.Temp);
    foreach (var edge in edges) {
        var slopeData = EntityManager.GetComponentData<NT_SlopeData>(edge);
        if (slopeData.HasPreview) {
            slopeData.HasPreview = false;
            EntityManager.SetComponentData(edge, slopeData);
        }
    }
    edges.Dispose();
}

// In OnStopRunning():
protected override void OnStopRunning() {
    CleanupHighlights();
    CleanupSlopeData();  // <-- Add this
    base.OnStopRunning();
}

// In CleanupHighlights() - also call CleanupSlopeData() or integrate:
protected virtual void CleanupHighlights() {
    // ... existing cleanup ...
    CleanupSlopeData();
}
```

### `NT_OverlayRenderSystem`

#### Before
```csharp
protected override void OnUpdate() {
    if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
        return;
    }

    if (tool.RenderEligibleNodes) {
        // schedule job
    }

    if (tool.RenderHandles) {
        // schedule job
    }

    if (tool.RenderEligibleEdges) {
        // schedule job
    }
    // ...
}
```

#### After
```csharp
protected override void OnUpdate() {
    if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
        return;
    }

    // ECS-driven: schedule if query has entities
    if (!m_NodeQuery.IsEmptyIgnoreFilter) {
        ScheduleDrawNodesJob();
    }

    if (!m_HandleQuery.IsEmptyIgnoreFilter) {
        ScheduleDrawHandlesJob();
    }

    if (!m_EdgeQuery.IsEmptyIgnoreFilter) {
        ScheduleDrawEdgesJob();
    }

    // Preview opt-in (still uses flags)
    if (tool.RenderTempEdges && !m_TempEdgeQuery.IsEmptyIgnoreFilter) {
        ScheduleDrawTempEdgesJob();
    }

    if (tool.RenderTempNodes && !m_TempNodeQuery.IsEmptyIgnoreFilter) {
        ScheduleDrawTempNodesJob();
    }
}
```

### `NT_UITooltipSystem`

#### Before
```csharp
private void UpdateEdgeTooltips(NT_BaseToolSystem tool) {
    if (tool.RenderSlopeTooltips) {
        ProcessEdgeTooltips(activeEdges);
    }
}

private (float slopePercent, float2 position) CalculateEdgeSlopeData(...) {
    // Recalculates slope from curve geometry
}
```

#### After
```csharp
// Two queries needed: one for original edges with slope data, one for Temp edges
private EntityQuery m_EdgesWithSlopeDataQuery;
private EntityQuery m_TempEdgesQuery;

// In OnCreate():
m_EdgesWithSlopeDataQuery = SystemAPI.QueryBuilder()
    .WithAll<Edge, NT_SlopeData>()
    .WithNone<Temp>()
    .Build();

m_TempEdgesQuery = SystemAPI.QueryBuilder()
    .WithAll<Edge, Temp>()
    .Build();

private void UpdateEdgeTooltips(NT_BaseToolSystem tool) {
    // ECS-driven: process if any edges with slope data exist
    // (originals have NT_SlopeData; Temps reference originals)
    if (!m_EdgesWithSlopeDataQuery.IsEmptyIgnoreFilter) {
        ProcessEdgeTooltips();
    }
}

private void ProcessEdgeTooltips() {
    var activeEdges = new NativeHashSet<Entity>(32, Allocator.Temp);

    // Process original edges with slope data
    var originalEdges = m_EdgesWithSlopeDataQuery.ToEntityArray(Allocator.Temp);
    foreach (var edge in originalEdges) {
        var curve = EntityManager.GetComponentData<Curve>(edge);
        if (curve.m_Length < MinCurveLength) continue;

        activeEdges.Add(edge);
        var (slope, position) = GetEdgeSlopeData(edge, curve, isTemp: false);
        AddTooltip(edge, slope, position, isTemp: false);
    }
    originalEdges.Dispose();

    // Process Temp edges that reference originals with slope data
    var tempEdges = m_TempEdgesQuery.ToEntityArray(Allocator.Temp);
    foreach (var tempEdge in tempEdges) {
        var temp = EntityManager.GetComponentData<Temp>(tempEdge);

        // Only show tooltip if original has slope data with preview
        if (!EntityManager.TryGetComponent<NT_SlopeData>(temp.m_Original, out var slopeData)) continue;
        if (!slopeData.HasPreview) continue;

        var curve = EntityManager.GetComponentData<Curve>(tempEdge);
        if (curve.m_Length < MinCurveLength) continue;

        activeEdges.Add(tempEdge);
        var (slope, position) = GetEdgeSlopeData(tempEdge, curve, isTemp: true);
        AddTooltip(tempEdge, slope, position, isTemp: true);
    }
    tempEdges.Dispose();

    CleanupStaleEntries(m_EdgeTooltipCache, activeEdges);
    activeEdges.Dispose();
}

/// <summary>
/// Gets slope data for an edge. Uses defensive lookups to handle edge cases.
/// </summary>
/// <returns>Slope and position, or null if data unavailable.</returns>
private (float slopePercent, float2 position)? TryGetEdgeSlopeData(Entity edgeEntity, Curve curve, bool isTemp) {
    float slope;

    if (isTemp) {
        // Temp entity: look up original
        if (!EntityManager.TryGetComponent<Temp>(edgeEntity, out var temp)) {
            return null;
        }
        if (!EntityManager.TryGetComponent<NT_SlopeData>(temp.m_Original, out var slopeData)) {
            return null; // Original doesn't have slope data
        }
        if (!slopeData.HasPreview) {
            return null; // Preview not yet calculated
        }
        slope = slopeData.PreviewSlopePercent;
    } else {
        if (!EntityManager.TryGetComponent<NT_SlopeData>(edgeEntity, out var slopeData)) {
            return null;
        }
        slope = slopeData.CurrentSlopePercent;
    }

    var position = WorldToTooltipPos(MathUtils.Position(curve.m_Bezier, 0.5f));
    position.y += isTemp ? TempTooltipYOffset : -TempTooltipYOffset;

    return (slope, position);
}
```

**Note**: We use `TryGetComponent` for defensive lookups. This handles:
- Temp entities whose originals were deselected
- Edges selected before `ShowSlopeTooltips` was enabled
- Race conditions during cleanup

### `ShapeTransformConfig`

#### Keep (Renamed for Clarity)
```csharp
/// <summary>
/// Whether this operation should display slope tooltips.
/// When true, tool will add NT_SlopeData components to selected edges.
/// </summary>
public bool ShowSlopeTooltips;
```

This is an **intent flag**, not a render flag. It tells the tool to add `NT_SlopeData` components.

### `NT_RoadShapeToolSystem` (and other tools)

#### Before
```csharp
// In OnCreate():
RenderEligibleNodes = true;
RenderHandles = true;
```

#### After
```csharp
// In OnCreate():
// Remove these lines - ECS components drive rendering now
// RenderEligibleNodes = true;  // DELETE
// RenderHandles = true;         // DELETE

// Keep preview flags if needed:
RenderTempEdges = true;
RenderTempNodes = true;
```

#### Add Slope Data Management
```csharp
/// <summary>
/// Calculates slope from curve data using traversal direction.
/// </summary>
private float CalculateSlope(Entity edgeEntity) {
    var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
    var (actualStart, _) = DetermineTraversalDirection(edge);

    bool isForward = (actualStart == edge.m_Start);
    float startY = isForward ? curve.m_Bezier.a.y : curve.m_Bezier.d.y;
    float endY = isForward ? curve.m_Bezier.d.y : curve.m_Bezier.a.y;

    return (endY - startY) / curve.m_Length * 100f;
}

/// <summary>
/// Calculates preview slope from the corresponding Temp entity's curve.
/// </summary>
private float CalculatePreviewSlope(Entity originalEdge, Entity tempEdge) {
    var tempCurve = EntityManager.GetComponentData<Curve>(tempEdge);
    var edge = EntityManager.GetComponentData<Edge>(originalEdge);
    var (actualStart, _) = DetermineTraversalDirection(edge);

    bool isForward = (actualStart == edge.m_Start);
    float startY = isForward ? tempCurve.m_Bezier.a.y : tempCurve.m_Bezier.d.y;
    float endY = isForward ? tempCurve.m_Bezier.d.y : tempCurve.m_Bezier.a.y;

    return (endY - startY) / tempCurve.m_Length * 100f;
}

// When selecting edges (if config wants slope tooltips):
private void SelectEdge(Entity edgeEntity) {
    EntityManager.AddComponentData(edgeEntity, new NT_Selected { ... });

    if (m_ActiveConfig.ShowSlopeTooltips) {
        var slope = CalculateSlope(edgeEntity);
        EntityManager.AddComponentData(edgeEntity, new NT_SlopeData {
            CurrentSlopePercent = slope,
            HasPreview = false
        });
    }
}

// When deselecting an edge:
private void DeselectEdge(Entity edgeEntity) {
    EntityManager.RemoveComponent<NT_Selected>(edgeEntity);

    // Also remove slope data if present
    if (EntityManager.HasComponent<NT_SlopeData>(edgeEntity)) {
        EntityManager.RemoveComponent<NT_SlopeData>(edgeEntity);
    }
}

// When Temp entities are created (after transformation job):
private void UpdatePreviewSlopes(NativeArray<Entity> tempEdges) {
    // Build lookup: original -> temp
    var originalToTemp = new NativeHashMap<Entity, Entity>(tempEdges.Length, Allocator.Temp);
    foreach (var tempEdge in tempEdges) {
        var temp = EntityManager.GetComponentData<Temp>(tempEdge);
        originalToTemp.TryAdd(temp.m_Original, tempEdge);
    }

    // Update slope data on originals
    var edgesWithSlope = m_EdgesWithSlopeDataQuery.ToEntityArray(Allocator.Temp);
    foreach (var originalEdge in edgesWithSlope) {
        if (originalToTemp.TryGetValue(originalEdge, out var tempEdge)) {
            var slopeData = EntityManager.GetComponentData<NT_SlopeData>(originalEdge);
            slopeData.PreviewSlopePercent = CalculatePreviewSlope(originalEdge, tempEdge);
            slopeData.HasPreview = true;
            EntityManager.SetComponentData(originalEdge, slopeData);
        }
    }

    edgesWithSlope.Dispose();
    originalToTemp.Dispose();
}

// When preview is cancelled (e.g., right-click or escape):
private void OnPreviewCancelled() {
    ClearPreviewState();  // Inherited from NT_BaseToolSystem
    // ... destroy Temp entities ...
}

// When config changes mid-operation:
private void OnConfigChanged(ShapeTransformConfig newConfig) {
    if (newConfig.ShowSlopeTooltips && !m_ActiveConfig.ShowSlopeTooltips) {
        // Newly enabled: add slope data to selected edges
        foreach (var edge in m_SelectedEdges) {
            if (!EntityManager.HasComponent<NT_SlopeData>(edge)) {
                EntityManager.AddComponentData(edge, new NT_SlopeData {
                    CurrentSlopePercent = CalculateSlope(edge),
                    HasPreview = false
                });
            }
        }
    } else if (!newConfig.ShowSlopeTooltips && m_ActiveConfig.ShowSlopeTooltips) {
        // Newly disabled: remove slope data
        CleanupSlopeData();  // Inherited from NT_BaseToolSystem
    }

    m_ActiveConfig = newConfig;
}
```

---

## Migration Checklist (Pipeline-Integrated Approach)

### Phase 1: Extend Data Structures
- [ ] Add `CurrentSlopePercent` field to `EdgeState`
- [ ] Create `NT_SlopeData.cs` component
- [ ] Add `NativeArray<float> m_PreviewSlopes` field to `NT_RoadShapeToolSystem`

### Phase 2: Update Jobs
- [ ] `GatherPathDataJob`: Compute `CurrentSlopePercent` for each `EdgeState`
- [ ] `ShapeTransformJob`: Add `PreviewSlopes` output array, populate after transformation

### Phase 3: Add Sync Points (3 locations only)
- [ ] `RefreshPathData()`: Add `SyncSlopeDataToEntities()` call after job completes
- [ ] `Update()`: Add `SyncPreviewSlopesToEntities()` call after preview job completes
- [ ] `Clear()`: Add `ClearPreviewState()` call

### Phase 4: Update `NT_BaseToolSystem`
- [ ] Remove `RenderEligibleEdges`, `RenderEligibleNodes`, `RenderHandles`, `RenderSlopeTooltips`
- [ ] Keep `RenderTempEdges`, `RenderTempNodes`
- [ ] Add `m_EdgesWithSlopeDataQuery`
- [ ] Add `CleanupSlopeData()` and `ClearPreviewState()` methods
- [ ] Call `CleanupSlopeData()` from `OnStopRunning()` / `CleanupHighlights()`

### Phase 5: Update `NT_OverlayRenderSystem`
- [ ] Replace flag checks with `IsEmptyIgnoreFilter` checks
- [ ] Keep flag checks only for Temp rendering

### Phase 6: Update `NT_UITooltipSystem`
- [ ] Add two queries: `m_EdgesWithSlopeDataQuery` (originals) and `m_TempEdgesQuery` (temps)
- [ ] Replace `tool.RenderSlopeTooltips` check with query emptiness check
- [ ] Replace `CalculateEdgeSlopeData` with `TryGetEdgeSlopeData` using defensive lookups
- [ ] Filter Temp tooltips by `HasPreview == true`

### Phase 7: Update `ShapeTransformConfig`
- [ ] Rename `RenderSlopeTooltips` to `ShowSlopeTooltips` (optional, clarifies intent)

### Phase 8: Testing
- [ ] Verify overlay rendering works without flags
- [ ] Verify slope tooltips display correctly for original edges
- [ ] Verify slope tooltips display correctly for Temp (preview) edges
- [ ] Verify preview slopes update correctly when transformation changes
- [ ] Verify `HasPreview` resets when preview is cancelled
- [ ] Verify cleanup on tool stop removes all `NT_SlopeData`
- [ ] Verify no exceptions when Temp entity references edge without `NT_SlopeData`

---

## Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| Temp entity's original was deselected | `TryGetComponent` returns false → skip tooltip |
| Edge selected before `ShowSlopeTooltips` enabled | No `NT_SlopeData` → skip tooltip |
| Config toggled ON mid-operation | Add `NT_SlopeData` to all selected edges |
| Config toggled OFF mid-operation | Remove all `NT_SlopeData` via `CleanupSlopeData()` |
| Preview cancelled (right-click) | `ClearPreviewState()` sets `HasPreview = false` |
| Tool deactivated | `OnStopRunning()` calls `CleanupSlopeData()` |
| Edge too short (`< MinCurveLength`) | Skip tooltip (checked in `ProcessEdgeTooltips`) |
| Multiple rapid preview updates | Latest `PreviewSlopePercent` overwrites previous |

---

## Thread Safety Notes

All `EntityManager` operations in this design run on the **main thread**. This is acceptable because:
1. Tooltip rendering is UI work (already main thread)
2. Component add/remove in tools happens during `OnUpdate` (main thread)

If performance becomes an issue with large selections, consider:
- Using `ComponentLookup<NT_SlopeData>` with jobs for read-only access
- Batching component operations with `EntityCommandBuffer`

---

## Summary

| Before | After |
|--------|-------|
| 6 boolean flags on base tool | 2 flags (preview opt-in only) |
| Flags gate job scheduling | ECS query emptiness gates jobs |
| Tooltip recalculates slopes | Tooltip reads `NT_SlopeData` |
| Config has render flags | Config has intent flags |
| Mixed responsibility | Clear ECS-driven architecture |
