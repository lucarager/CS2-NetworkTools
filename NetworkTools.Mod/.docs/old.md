# Handle System Refactor

Design document for moving handle declaration to the same inline-on-tool, reflection-driven pattern as the parameter system. Written 2026-05-04.

Companion doc: the parameter system this extends is documented in `parameter-system-refactor.md` — read it if anything about parameter discovery, Modes bitflag, or OnChanged events is unclear.

---

## 1. Context

### Current architecture

Handles are ECS entities owned by the active tool system. Their lifecycle (creation, raycast, drag dispatch, cleanup) lives in `BaseToolSystem.Handles.cs`. Each tool/mode supplies an array of `TransformHandleDefinition` structs that the base system materializes into entities.

```
Tool System
|
+-- Parameters (inline, reflection-discovered)        <-- done in parameter refactor
|
+-- Generators (per mode, IGenerator/IConnectionGenerator/IPathTransformation)
|       BuildHandleDefinitions(in JobConfig, dictionary<string, ParameterBase>)
|              ↓
+-- TransformHandleDefinition[] -> CreateHandlesFromDefinitions
|              ↓
+-- ECS handle entities (NT_Handle + NT_HandlePosition + ...)
|              ↓
+-- Drag callbacks (OnPositionHandleDragged, OnCircleHandleDragged,
                   OnRotationHandleDragged, OnParameterHandleDragged)
```

Relevant files:

- `Systems/Tools/Base/TransformHandleDefinition.cs` — definition struct
- `Systems/Tools/Base/BaseToolSystem.Handles.cs` — lifecycle, raycast, drag dispatch (~1600 lines)
- `Components/Handles/NT_Handle.cs` + siblings — ECS components
- `Systems/Tools/Connect/Generators/*.cs` — `BuildHandleDefinitions` per mode
- `Systems/Tools/Generate/Generators/*.cs` — same pattern
- `Systems/Tools/RoadShape/Transforms/*.cs` — same pattern
- `Systems/Tools/RoadShape/RoadShapeToolSystem.Handles.cs` — `OnParameterHandleDragged` override with object-identity dispatch

### What was achieved by the parameter refactor

Handles already use direct `ParameterBase` references on `TransformHandleDefinition.Parameter`. `HandleKeys` enums and key-based dispatch switches are gone. `OnCircleHandleDragged` / `OnRotationHandleDragged` write the mapped parameter directly via `m_HandleParameterMap`. Half the work is done.

### Pain points still remaining

1. **String-keyed parameter lookup.** `BuildHandleDefinitions` receives `IReadOnlyDictionary<string, ParameterBase>` and does `parameters["connect.curveStartControlPointPosition"]`. Drift-prone, redundant — the tool already has each parameter as a typed field.
2. **Object-identity dispatch in overrides.** RoadShape's `OnParameterHandleDragged` does `if (param == EaseInLength) ... else if (param == EaseOutLength) ...`. The same dispatch-switch shape the parameter refactor killed elsewhere, just keyed on object identity.
3. **Parent-child via integer keys.** `Key = 2`, `ParentKey = 1` — arbitrary local constants whose only purpose is naming siblings within one definition set.
4. **Two declarations sites for one concept.** Parameter is declared on the tool. Handle that drives it is declared in a generator. To understand how a UI parameter and its on-canvas handle relate, you read two files.
5. **Generators do double duty.** Generators legitimately own mode-dispatched job geometry (Burst-mandated). They incidentally also own handle construction, which has no Burst constraint and doesn't need mode dispatch in C#.

### What is *not* a problem (do not change)

- **Mode dispatch in jobs.** `switch (Mode)` inside Burst jobs stays — same constraint as parameters.
- **Generator interfaces (`IGenerator`, `IConnectionGenerator`, `IPathTransformation`).** Mode-specific *job geometry* lives in the right place.
- **ECS handle entities.** The runtime representation (`NT_Handle` + position + optional geometric components) is fine. Only the *authoring* surface changes.
- **Raycast hit-testing in `BaseToolSystem.Handles.cs`.** Type-aware intersection (sphere/line/circle) stays.
- **The drag input state machine** (`Idle` → `PendingAction` → `Dragging`). Stays as-is; only what happens *inside* a drag tick changes.

---

## 2. Goals

- Declare each tool's handles once, inline on the tool system, next to the parameters they drive.
- Filter handles by active mode via the same `Modes` bitflag the parameter system uses.
- Replace string-keyed parameter lookup with `nameof(...)` field references resolved at discovery time.
- Replace object-identity dispatch in overrides with declarative `ComputeFromPosition` / `ComputePosition` delegates on the handle declaration.
- Replace integer parent keys with `nameof(...)` parent references.
- Generators stop authoring handles entirely; they keep mode-dispatched job geometry.
- Same mental model as parameters: declare, tag with modes, let the base system wire it.

### Non-goals

- Changing the ECS runtime representation of handles.
- Eliminating mode-dispatched job geometry in generators.
- Replacing the raycast / drag state machine.
- Generalizing "handles are parameters." They are not — handles are a spatial view layer over parameters with their own lifecycle and shape choices. The win is making the *binding* declarative, not collapsing the concepts.

(Parent → child propagation behavior is preserved end-to-end, but the *mechanism* changes from imperative per-frame walk to a one-time subscription wired at creation. See §9.2.)

---

## 3. Architecture Overview

```
Tool System (class, declared in C#)
|
+-- Parameters (fields)             <-- already done
|
+-- Handles (fields)                <-- new: declarative, reflection-discovered
|   |
|   +-- parameter (by nameof)
|   +-- modes (bitflag, same vocabulary as parameters)
|   +-- visual hints (radius, type flags, constraints, normal, ...)
|   +-- optional ComputeFromPosition / ComputePosition delegates
|   +-- optional parent (by nameof)
|
+-- Generators (per mode, unchanged)
|       Now: only job geometry. BuildHandleDefinitions removed.
|
+-- Job snapshot                    <-- already done
```

What goes away:
- `BuildHandleDefinitions(...)` static methods on every generator.
- `IReadOnlyDictionary<string, ParameterBase>` parameter lookup at handle construction.
- `RoadShapeToolSystem.Handles.cs` override file (entire file deletes).
- Integer `Key` / `ParentKey` authoring on the generator side.

What stays (intentionally):
- ECS handle entities and components.
- Raycast hit-testing and drag state machine in `BaseToolSystem.Handles.cs`.
- Mode dispatch in jobs.
- Generator interfaces.
- Internal entity-resolution keys inside `CreateHandlesFromDefinitions` (an implementation detail; just no longer authored by hand).

---

## 4. Handle Definition Base Classes

### Hierarchy

```csharp
public abstract class HandleDefinition {
    public string Key      { get; }      // resolved from field name at discovery time
    public string ParameterName { get; } // nameof(...) — resolved to ParameterBase at discovery
    public string ParentName { get; }    // nameof(...) or null
    public int    Modes    { get; }      // bitflag, 0 = all modes
    public float  Radius   { get; }
    public HandleTypeFlags TypeFlags { get; }

    // Resolved by the base system at discovery time.
    public ParameterBase Parameter { get; internal set; }
    public HandleDefinition Parent { get; internal set; }

    // Optional value derivation. When non-null, the base system uses these
    // instead of writing position/value directly.
    public ComputeValueFromPosition ComputeFromPosition { get; }
    public ComputePositionFromValue ComputePosition     { get; }
}

public delegate float ComputeValueFromPosition(
    NT_BaseToolSystem tool, Entity handle, float3 worldPos);

public delegate float3 ComputePositionFromValue(
    NT_BaseToolSystem tool, Entity handle, float value);

public class PositionHandleDef       : HandleDefinition { public NT_HandleConstraints? Constraints; }
public class BezierControlHandleDef  : HandleDefinition { /* visual variant of Position */ }
public class CircleHandleDef         : HandleDefinition { public float3 Normal; }
public class RotationHandleDef       : HandleDefinition { public float3 Normal, ReferenceDirection; }
```

### Design principle: declaration site = tool system

All handles are declared as `public` fields on the concrete tool system, **next to the parameter they drive**. There is no separate handle-definition class, no per-mode generator method.

- Field name becomes the handle's `Key` (via reflection at discovery).
- `parameter:` arg uses `nameof(SomeParameter)` — checked at compile time, resolved once at discovery.
- `parent:` arg uses `nameof(OtherHandle)` — same resolution.
- `Modes` bitflag uses the same enum the parameter system already uses for that tool.

### Handle management lives on `NT_BaseToolSystem`

Mirror the parameter pattern. New partial: `BaseToolSystem.HandleSchema.cs`.

```csharp
public abstract partial class NT_BaseToolSystem {
    private HandleDefinition[] m_HandleDefinitions;

    public IReadOnlyList<HandleDefinition> HandleDefinitions =>
        m_HandleDefinitions ??= HandleSchema.Discover(this);

    // Filtered to handles that apply to the active mode.
    protected HandleDefinition[] GetActiveHandleDefinitions() {
        var modeFlag = GetActiveModeFlag(); // tool-specific, returns 0 for mode-less tools
        return HandleDefinitions
            .Where(h => h.Modes == 0 || (h.Modes & modeFlag) != 0)
            .ToArray();
    }
}
```

`HandleSchema.Discover(object instance)` reflects `public` instance fields whose type is `HandleDefinition`, caches `FieldInfo[]` per concrete type, and resolves the `ParameterName` / `ParentName` strings to actual references in a second pass (so forward refs work). Same caching strategy as `ParameterSchema`.

### Handle declaration example (Parallel — mode-less)

Parallel has no handles today; included for shape illustration only. Skip during migration.

```csharp
public partial class NT_ParallelToolSystem {
    public FloatParameter HorizontalOffset = new("parallel.horizontalOffset", 20f, 0f, 80f);

    // Hypothetical: a slider-equivalent drag handle on the path.
    public PositionHandleDef HorizontalOffsetHandle = new(
        parameter: nameof(HorizontalOffset),
        constraints: HandleConstraints.AxisX);
}
```

### Handle declaration example (Connect — mode-filtered, parent-child)

```csharp
public partial class NT_ConnectToolSystem : NT_BaseToolSystem {
    public EnumParameter<ConnectMode> Mode = new("connect.mode", ConnectMode.None);

    public Float3Parameter StartPosition = new("connect.startPosition", default);
    public Float3Parameter CurveStartControlPointPosition = new(
        "connect.curveStartControlPointPosition", default,
        modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve);
    public FloatParameter LoopRadius = new("connect.loopRadius", 50f, 1f, 500f,
        modes: (int)ConnectMode.Loop);

    public PositionHandleDef StartHandle = new(
        parameter: nameof(StartPosition));

    public BezierControlHandleDef CurveStartCtlHandle = new(
        parameter: nameof(CurveStartControlPointPosition),
        parent:    nameof(StartHandle),
        modes:     (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve,
        radius:    NT_Handle.SecondaryRadius);

    public CircleHandleDef LoopCircleHandle = new(
        parameter: nameof(LoopRadius),
        modes:     (int)ConnectMode.Loop);
}
```

### Handle declaration example (RoadShape — computed)

```csharp
public partial class NT_RoadShapeToolSystem {
    public FloatParameter EaseInLength = new("roadShape.easeInLength", 0.3f, 0f, 1f,
        modes: (int)ShapeMode.SlopeEaseInOut | (int)ShapeMode.CurveSmooth);

    public PositionHandleDef EaseInHandle = new(
        parameter:   nameof(EaseInLength),
        modes:       (int)ShapeMode.SlopeEaseInOut | (int)ShapeMode.CurveSmooth,
        constraints: HandleConstraints.AxisXZ,

        computeFromPosition: (tool, handle, pos) => {
            var t = (NT_RoadShapeToolSystem)tool;
            var pathXZ = t.EndPosition.Value.xz - t.StartPosition.Value.xz;
            var len    = math.length(pathXZ);
            if (len < 0.001f) return t.EaseInLength.Min;
            var axis   = math.normalize(pathXZ);
            var offset = pos.xz - t.StartPosition.Value.xz;
            return math.clamp(math.dot(offset, axis) / len, t.EaseInLength.Min, t.EaseInLength.Max);
        },

        computePosition: (tool, handle, value) => {
            var t = (NT_RoadShapeToolSystem)tool;
            return math.lerp(t.StartPosition.Value, t.EndPosition.Value, value);
        });
}
```

`RoadShapeToolSystem.Handles.cs` deletes entirely. The math moves onto the declaration; the override-and-compare ladder disappears.

---

## 5. Drag Dispatch (Generic)

Replace per-tool `OnParameterHandleDragged` overrides with a single generic path in `BaseToolSystem.Handles.cs`.

```csharp
void DispatchHandleDrag(Entity handle, float3 position, float rawValue) {
    if (!m_HandleDefMap.TryGetValue(handle, out var def)) return;

    switch (def) {
        case PositionHandleDef _:
        case BezierControlHandleDef _:
            if (def.Parameter is Float3Parameter f3p)
                f3p.Value = position;
            else if (def.Parameter is FloatParameter fp && def.ComputeFromPosition != null)
                fp.Value = def.ComputeFromPosition(this, handle, position);
            break;

        case CircleHandleDef _:
            if (def.Parameter is FloatParameter cfp)
                cfp.Value = rawValue; // raycast already computed radius
            break;

        case RotationHandleDef _:
            if (def.Parameter is Float3Parameter rf3p)
                rf3p.Value = /* unit direction on rotation plane at rawValue radians,
                                derived from NT_HandleRotation's ReferenceDirection */;
            break;
    }
}
```

`OnPositionHandleDragged` / `OnCircleHandleDragged` / `OnRotationHandleDragged` / `OnParameterHandleDragged` virtuals all collapse to one non-virtual dispatcher. Tools that need custom math supply delegates on the declaration; nobody overrides anything.

---

## 6. Reverse Sync (Parameter → Handle Position)

When a parameter changes from a non-handle source (slider drag, reset, programmatic), the handle entity's position must update.

Wire on tool activation / handle creation:

```csharp
foreach (var def in GetActiveHandleDefinitions()) {
    var entity = /* the created handle entity */;
    def.Parameter.OnChanged += () => {
        var newPos = def.ComputePosition != null
            ? def.ComputePosition(this, entity, ((FloatParameter)def.Parameter).Value)
            : ((Float3Parameter)def.Parameter).Value;
        SetHandlePosition(entity, newPos);
    };
}
```

This replaces today's `ShapeConfigRevision`-style counters and any ad-hoc "push handle position when slider moves" code in tool systems. Same `OnChanged` flow that the parameter refactor uses for UI bindings — the handle just becomes another subscriber.

Detach subscriptions on `CleanupHandles` to avoid leaks across tool activations.

### Bounce prevention

`Parameter<T>.Value` already short-circuits on equality, so the loop `handle drag → param.Value = ... → OnChanged → SetHandlePosition` won't bounce as long as `ComputePosition(ComputeFromPosition(p)) ≈ p` for the projected case. Float epsilon needs care — pick a tolerance in the position-equality check, or skip reverse-sync while a drag is active on that handle.

---

## 7. Mode Activation: Rebuilding the Active Set

When `EnumParameter<TMode> Mode.OnChanged` fires:

1. Destroy existing handle entities and detach their `OnChanged` subscriptions.
2. Rebuild via `CreateHandlesFromDefinitions(GetActiveHandleDefinitions())`.
3. Wire reverse-sync (§6) and parent-child (§9.2) subscriptions for the new active set.

`CleanupHandles` already destroys handle entities on tool *stop*; mode-change is new wiring that runs the same destroy step plus rebuild. Subscribe to `Mode.OnChanged` once in `OnCreate`. Each tool implements:

```csharp
protected abstract int GetActiveModeFlag();
// Parallel:    return 0; (no modes)
// Connect:     return (int)Mode.Value;
// Generate:    return (int)Mode.Value;
// RoadShape:   return (int)Mode.Value;
```

Mode-less tools return 0; the bitflag filter then matches any handle with `Modes == 0` (i.e., all of them).

---

## 8. Generator Cleanup

After this refactor, generators do *only* job geometry:

```csharp
public struct SimpleCurveGenerator : IConnectionGenerator {
    public void InitializeConfig(ref ConnectJobConfig config) { /* seed positions */ }
    public void GenerateConnection(in ConnectJobConfig config, ref NativeList<CurveDef> curves) { ... }
    // BuildHandleDefinitions: GONE
}
```

The `IReadOnlyDictionary<string, ParameterBase>` plumbing through `Connect.Handles.cs` / `Generate.Handles.cs` deletes. The tool no longer asks the active generator for handles — it asks the base system, which reads its own declarations.

---

## 9. Adjacent Improvements

Four cleanups that fit the same "declare once, base wires it via events" theme. They share files with the core refactor and should ride along rather than be deferred.

### 9.1 Auto-wire `m_UpdateNeeded` from parameter `OnChanged`

Every tool's `OnCreate` currently does:

```csharp
HorizontalOffset.OnChanged    += () => m_UpdateNeeded = true;
VerticalOffset.OnChanged      += () => m_UpdateNeeded = true;
HorizontalDirection.OnChanged += () => m_UpdateNeeded = true;
// ... one line per parameter
```

Discovery already iterates every parameter field. Move the wiring there:

```csharp
// BaseToolSystem.Parameters.cs — extend the discovery step
foreach (var p in Parameters) {
    p.OnChanged += MarkUpdateNeeded;
}

protected void MarkUpdateNeeded() => m_UpdateNeeded = true;
```

A tool that needs *additional* side-effects on a specific parameter still subscribes manually; the default just stops being boilerplate.

### 9.2 Parent → child propagation via subscription, not imperative call

Today's `HandlePositionDrag` does:

```csharp
f3p.Value = handlePos;
if (math.lengthsq(delta) > 0f) {
    PropagateToChildren(handle, delta);    // imperative walk
    SyncChildParameterPositions(handle);   // O(n²) re-scan all handles
}
```

`SyncChildParameterPositions` ([BaseToolSystem.Handles.cs:808-825](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:808)) iterates every handle in the tool to find children of one parent. With the new `HandleDefinition` graph, the parent-child relationship is known at discovery. Wire it as a subscription at creation time:

```csharp
// In CreateHandlesFromDefinitions, after entities exist:
foreach (var def in defs) {
    if (def.Parent == null) continue;

    var childEntity = entityByDef[def];
    var parentParam = def.Parent.Parameter as Float3Parameter;
    var childParam  = def.Parameter as Float3Parameter;
    if (parentParam == null || childParam == null) continue;

    var lastParentPos = parentParam.Value;
    parentParam.OnChanged += () => {
        var delta = parentParam.Value - lastParentPos;
        lastParentPos = parentParam.Value;
        if (math.lengthsq(delta) < 1e-8f) return;

        childParam.Value += delta;
        // Child's §6 reverse-sync subscription fires on this OnChanged
        // and moves the child handle entity. No explicit position write needed.
    };
}
```

`PropagateToChildren` and `SyncChildParameterPositions` both delete. The hierarchy walk happens once at wire time; per-frame cost drops from O(parents × all_handles) to O(direct_children). The handle-entity update piggybacks on the §6 reverse-sync we already wired for every parameter-bound handle.

This is core to the new pattern, not optional — it eliminates `m_HandleParameterMap` lookups during drag and replaces an imperative one-shot call with a subscription that fires only when state actually changes. Land in Phase 1 alongside the foundation.

### 9.3 Lifecycle hooks as events, not virtuals

Today `OnHandleDragStart` / `OnHandleDragEnd` / `OnHandleClick` are protected virtuals each tool overrides one at a time. Multiple subscribers can't coexist (overlay system, undo stack, audio cues all want notification).

```csharp
public event Action<Entity> HandleDragStarted;
public event Action<Entity> HandleDragEnded;
public event Action<Entity> HandleClicked;

// Inside the drag state machine:
HandleDragStarted?.Invoke(m_DraggedHandle);
```

Tools that overrode the virtual subscribe in `OnCreate` instead. Other systems can subscribe without forcing inheritance. Same shape as `Parameter.OnChanged`.

### 9.4 Single map: entity → `HandleDefinition`

Today there's `m_HandleParameterMap` (entity → parameter). After the handle refactor, the natural primary key is the definition itself. Replace with:

```csharp
protected Dictionary<Entity, HandleDefinition> m_HandleDefMap;
```

Parameter access is `def.Parameter`; parent is `def.Parent`; constraints are `def.Constraints`. One map, one lookup, all the metadata is reachable from any entity.

---

## 10. Migration Plan

### Phase 1 — Foundation + Connect migration

Connect is the validation target: 3 modes, multi-parameter handles, parent-child relationships, mode-filtered handles. If it works on Connect, it works everywhere.

- Implement `HandleDefinition`, `PositionHandleDef`, `BezierControlHandleDef`, `CircleHandleDef`, `RotationHandleDef` in `Systems/Tools/Handles/`.
- Implement `HandleSchema.Discover(object)` with cached per-type `FieldInfo[]` and two-pass name resolution (parameters first, then parent refs).
- Add `BaseToolSystem.HandleSchema.cs` partial with `HandleDefinitions`, `GetActiveHandleDefinitions()`.
- Add abstract `GetActiveModeFlag()` to `NT_BaseToolSystem` (default impl returns 0).
- Implement generic drag dispatcher in `BaseToolSystem.Handles.cs`. Keep the old virtuals temporarily, route through new path.
- Wire reverse-sync `OnChanged` subscriptions in `CreateHandlesFromDefinitions`.
- Wire parent → child propagation as `OnChanged` subscriptions (§9.2). Delete `PropagateToChildren` and `SyncChildParameterPositions` once Connect drives parent-child purely via subscriptions.
- Replace `m_HandleParameterMap` with `m_HandleDefMap` (entity → `HandleDefinition`, §9.4). Old map can stay alongside during Phase 1; cull in Phase 3 cleanup.
- Auto-subscribe `MarkUpdateNeeded` to every discovered parameter in `BaseToolSystem.Parameters.cs` (§9.1). Remove the manual `OnChanged += () => m_UpdateNeeded = true` lines from Connect's `OnCreate`.
- Migrate Connect:
  - Declare `StartHandle`, `EndHandle`, `CurveStartCtlHandle`, `CurveEndCtlHandle`, `LoopCircleHandle`, `ComplexCtl1Handle`, `ComplexCtl2Handle` inline on `NT_ConnectToolSystem`.
  - Override `GetActiveModeFlag` to return `(int)Mode.Value`.
  - Remove `BuildHandleDefinitions` from `SimpleCurveGenerator`, `LoopGenerator`, `ComplexCurveGenerator`.
  - Delete `Connect.Handles.cs` orchestration that asked the generator for handles.

End-to-end: mode change → handle set rebuild → drag → parameter update → UI binding. Job code unchanged.

### Phase 2 — RoadShape (computed handles)

Front-loads the only remaining novel feature: `ComputeFromPosition` / `ComputePosition` delegates. Doing this before Generate validates the compute path while the dual-dispatch shim is still in place and rollback is cheap. Generate adds nothing Phase 1 didn't already prove and is deferred to Phase 3 with cleanup.

- Implement compute-delegate path in `BaseToolSystem.Handles.cs` drag dispatcher (sketch in §5).
- Implement reverse-sync via `ComputePosition` for `FloatParameter`-bound position handles (§6).
- Declare `EaseInHandle`, `EaseOutHandle` (and any others) inline on `NT_RoadShapeToolSystem` with their projection delegates.
- Move math from `RoadShapeToolSystem.Handles.cs` onto the declarations.
- Delete `RoadShapeToolSystem.Handles.cs`.
- Remove `BuildHandleDefinitions` from `SlopeEaseInOutTransform`, `SlopeLinearTransform`, `SlopeArchTransform`, `CurveSmoothTransform`.
- Override `GetActiveModeFlag` to return `(int)Mode.Value`.
- Verify against the gotchas in §11.5–§11.6 (template presets fire reverse-sync correctly; bounce prevention holds for projection math).

### Phase 3 — Generate + Cleanup

Generate is mechanical (same shape as Connect, no new code paths). Bundle with the cleanup deletions because (a) Generate has no architectural risk, and (b) the deletions need every tool migrated before they're safe.

**Generate migration:**
- Declare handles inline on `NT_GenerateToolSystem`. Modes: Grid, Circle. Handles: origin position, circle radius (`CircleHandleDef`).
- Remove `BuildHandleDefinitions` from `GridGenerator`, `CircleGenerator`.
- Override `GetActiveModeFlag` to return `(int)Mode.Value`.
- Delete `Generate.Handles.cs`.

**Cleanup (after Generate is on the new path):**
- Delete `OnParameterHandleDragged`, `OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged` virtuals from `NT_BaseToolSystem`.
- Convert `OnHandleDragStart` / `OnHandleDragEnd` / `OnHandleClick` virtuals to `event Action<Entity>` (§9.3). Tools that overrode them subscribe in `OnCreate`.
- Delete `m_HandleParameterMap`; `m_HandleDefMap` is now the single source of truth (§9.4).
- Delete the `CreateHandlesFromDefinitions(TransformHandleDefinition[])` entry point. Either fold `TransformHandleDefinition` into `HandleDefinition` or keep it as an internal struct emitted by `HandleDefinition.ToTransformDef(tool)` (decided in Phase 1 per §11.3).
- Delete `IReadOnlyDictionary<string, ParameterBase>` plumbing.
- Delete any remaining revision counters tied to handle drags.
- Verify no manual `OnChanged += () => m_UpdateNeeded = true` lines remain in tool `OnCreate` methods (§9.1 — auto-wired in foundation).
- Update this doc with final shape.

---

## 11. Open Questions / Risks

### General

1. **Reverse-sync float epsilon.** When `ComputeFromPosition(ComputePosition(v)) ≠ v` exactly, `OnChanged` fires twice per drag tick. Use a small tolerance in the position-equality check, or skip reverse-sync while the parameter's *own* handle is mid-drag. Decide during Phase 1.

2. **Discovery cost.** `HandleSchema.Discover` runs once per concrete tool type and caches. Same shape as `ParameterSchema` and not a hot path. No mitigation needed.

3. **`TransformHandleDefinition` lifetime.** Currently authored by generators, consumed by `CreateHandlesFromDefinitions`. Two options after migration:
   - Keep it as an *internal* struct emitted by `HandleDefinition.ToTransformDef(tool)`. Lower-risk.
   - Fold its fields into `HandleDefinition` and have `CreateHandlesFromDefinitions` take `HandleDefinition[]` directly. Cleaner; more churn in `BaseToolSystem.Handles.cs`.

   Pick during Phase 1; the cleaner option becomes worth doing once all tools migrate.

4. **`HandleConstraints.AxisXZ` etc.** Today `NT_HandleConstraints` is a struct authored inline. Add static helpers (`HandleConstraints.AxisXZ`, `HandleConstraints.Plane(normal)`, etc.) to make declarations readable.

### Tool-specific gotchas

5. **Connect contextual seeding.** `SimpleCurveGenerator.InitializeConfig` seeds `CurveStartControlPointPosition` from `StartPosition + StartDirection * length/3`. After this refactor, that seeding still happens in the generator — handle just reads the parameter once it's seeded. Verify ordering: `InitializeConfig` must run before `CreateHandlesFromDefinitions` on tool/mode activation. (Already true today; flag if Phase 1 reveals otherwise.)

6. **RoadShape template presets.** Per the parameter doc §10.8, `Preserve()` / `SlopeLinear()` / `CurveSmooth()` etc. reset config to template-specific defaults. Those are parameter-level operations and unaffected by this refactor — they fire `OnChanged` per parameter, which then triggers reverse-sync to move handles. Verify visually during Phase 2.

7. **Multi-parameter handles.** Today the only case is RoadShape's ease handles, where each handle is bound to one parameter (separate declarations). True multi-parameter handles (one drag → two parameters) aren't currently used. If introduced later, extend `HandleDefinition` with a `ComputeMultiFromPosition` returning a struct, or split into multiple coupled handles. Defer until needed.

### Migration-window concerns

8. **Co-existence of old and new dispatch.** During Phases 1–2, migrated tools use the new dispatch path while unmigrated tools still author via `BuildHandleDefinitions`. The base system needs to handle both shapes. Strategy: keep `CreateHandlesFromDefinitions(TransformHandleDefinition[])` as the inner primitive; add `CreateHandlesFromDefinitions(HandleDefinition[])` that translates and calls the inner one. Old tools call the first; new tools call the second. Remove the first in Phase 3 cleanup.

9. **`m_HandleParameterMap` ↔ `m_HandleDefMap`.** During migration, both maps may exist. Old dispatch uses the parameter map; new dispatch uses the def map (which contains the parameter via `def.Parameter`). Don't merge until Phase 3 cleanup.

---

## 12. Reference: Files Likely To Change

| Area | Files |
| --- | --- |
| New | `Systems/Tools/Handles/HandleDefinition.cs`, `PositionHandleDef.cs`, `BezierControlHandleDef.cs`, `CircleHandleDef.cs`, `RotationHandleDef.cs` |
| New | `Systems/Tools/Handles/HandleSchema.cs` |
| New | `Systems/Tools/Handles/HandleConstraints.cs` (static helpers) |
| New | `Systems/Tools/Base/BaseToolSystem.HandleSchema.cs` (partial — adds `HandleDefinitions`, `GetActiveHandleDefinitions`) |
| Modified | `Systems/Tools/Base/BaseToolSystem.cs` (add abstract `GetActiveModeFlag`) |
| Modified | `Systems/Tools/Base/BaseToolSystem.Parameters.cs` (auto-wire `MarkUpdateNeeded`, §9.1) |
| Modified | `Systems/Tools/Base/BaseToolSystem.Handles.cs` — Phase 1: generic drag dispatcher, reverse-sync wiring, parent-child subscription wiring (§9.2), single `m_HandleDefMap` (§9.4), dual-dispatch translation. Phase 3 cleanup: lifecycle events (§9.3). |
| Modified | `Systems/Tools/Connect/NT_ConnectToolSystem.cs` (Phase 1) |
| Modified | `Systems/Tools/Connect/Generators/*.cs` (Phase 1 — remove `BuildHandleDefinitions`) |
| Deleted | `Systems/Tools/Connect/Connect.Handles.cs` (Phase 1 — orchestration removed) |
| Modified | `Systems/Tools/RoadShape/NT_RoadShapeToolSystem.cs` (Phase 2) |
| Modified | `Systems/Tools/RoadShape/Transforms/*.cs` (Phase 2) |
| Deleted | `Systems/Tools/RoadShape/RoadShapeToolSystem.Handles.cs` (Phase 2) |
| Modified | `Systems/Tools/Generate/NT_GenerateToolSystem.cs` (Phase 3) |
| Modified | `Systems/Tools/Generate/Generators/*.cs` (Phase 3) |
| Deleted | `Systems/Tools/Generate/Generate.Handles.cs` (Phase 3) |
| Modified or Deleted | `Systems/Tools/Base/TransformHandleDefinition.cs` (Phase 3 cleanup — folded into `HandleDefinition` or kept internal) |

---

## 13. Quick Reference: Before / After

| Concern | Today | After |
| --- | --- | --- |
| Handle declaration site | Generator's `BuildHandleDefinitions` | Inline field on tool system |
| Parameter resolution | `parameters["string.key"]` lookup | `nameof(Field)` resolved at discovery |
| Mode filtering | `switch (Mode)` in generator | `Modes` bitflag on declaration |
| Parent reference | Integer key constants | `nameof(OtherHandle)` |
| Computed-handle math | Override + object-identity dispatch | `ComputeFromPosition` / `ComputePosition` delegates on declaration |
| Drag dispatch | Multiple virtual methods per tool | Single generic dispatcher in base |
| Parameter → handle position sync | Ad hoc / revision counters | `OnChanged` subscription in base |
| Generator's role | Mode-dispatched job geometry **and** handle authoring | Mode-dispatched job geometry only |
| Mode dispatch in jobs | `switch (Mode)` | Unchanged (Burst-mandated) |
| ECS handle entities | Created from `TransformHandleDefinition[]` | Same — internal authoring shape only |
| `m_UpdateNeeded` wiring | Manual `OnChanged += () => ...` per parameter in each tool's `OnCreate` | Auto-subscribed during parameter discovery |
| Parent → child propagation | Imperative `PropagateToChildren` + O(n²) `SyncChildParameterPositions` per drag tick | Subscription wired once at handle creation; fires only on actual change |
| Drag lifecycle hooks | `protected virtual` overrides (single subscriber) | `event Action<Entity>` (multi-subscriber) |
| Handle metadata maps | `m_HandleParameterMap` (entity → parameter) | `m_HandleDefMap` (entity → `HandleDefinition`, full metadata reachable) |
