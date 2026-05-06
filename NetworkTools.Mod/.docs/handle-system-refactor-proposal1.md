# Handle System Refactor (v2)

Nest handle declarations under their owning parameter, eliminate per-tool `BuildHandleDefinitions` methods, the string-keyed parameter lookup, and the residual per-type drag dispatch. Written 2026-05-04, revised 2026-05-06.

Supersedes earlier handle-refactor sketches in this directory.

---

## 1. Scope

### Design principles (use these to resolve ambiguity)

- **Co-locate handle metadata with the parameter it drives.** New metadata goes on the spec, not in a side-table or a generator method.
- **One authoring pattern across all tools.** Connect, RoadShape, Generate, Parallel all declare the same way. Don't add tool-specific dispatch paths.
- **Generators are geometry-only.** Anything that's "where in the world is this parameter" is a handle concern; anything that's "how do these parameters become curves/edges" is a generator concern.
- **Single-handle case stays simple.** Multi-handle support shouldn't bloat the common path. New features land as optional spec fields, not new required ones.
- **Compile-time over runtime where possible.** Prefer typed field references (`nameof`) over strings; prefer the type system enforcing spec/parameter compatibility over runtime checks.

### Out of scope (do not change)

- **Mode dispatch in jobs.** Burst forbids virtual dispatch — the `switch (Mode)` in jobs stays.
- **Generator geometry.** `GenerateConnection` / `Process` / `GetCurves` own the algorithm; untouched.
- **ECS handle entity model.** `NT_Handle`, `NT_HandlePosition`, `NT_HandleCircle`, `NT_HandleConstraints` etc. are load-bearing for raycast and render; untouched.
- **Raycast hit detection** and the drag input state machine in `BaseToolSystem.Handles.cs`. Only the dispatch layer above them changes.
- **Parameter system.** Mostly additive — `Handles` and `SetValue(T, ChangeOrigin)` are new; `OnChanged` signature changes from `Action` to `Action<ChangeOrigin>`.

---

## 2. Architecture

```
Parameter<T> (declared on tool system)
|
+-- value, range, modes, OnChanged           (existing)
|
+-- Handles : IHandleSpec<T>[]               <-- new: zero or more spatial views
    |
    +-- PositionHandle, CircleHandle, RotationHandle, ComputedPositionHandle
    +-- per-spec: TypeFlags, Constraints, Parent, Compute delegates
```

**Build (mode change / tool activation):** walk `Parameters[].Handles` inline, filter by active mode, materialize ECS handle entities, register each in `m_HandleEntries: Dictionary<Entity, HandleEntry>` (one entry holds parameter + spec).

**Drag:** raycast hits an entity → read `m_HandleEntries[entity]` → for `Float3Parameter`, `ComputeFromPosition` if present, otherwise direct position write; for `FloatParameter`, `ComputeFromPosition` always (built-in defaults for circle/rotation, required for computed) → `param.SetValue(v, ChangeOrigin.Handle)` → `OnChanged` fires with origin → subscribers react (reverse-sync skips `Handle` origin; parent-child, bindings, and `m_UpdateNeeded` fire normally).

**Reverse-sync:** permanent subscriber on each parameter (wired once during parameter discovery). On `OnChanged`, checks origin — skips `ChangeOrigin.Handle` (handle is already positioned by the drag), runs for `Code`. Reads `m_ParameterHandles` lookup (rebuilt on mode change) to find active handle entities, delegates to `spec.SyncToEntity` which updates the appropriate ECS component per handle type.

**Parent-child propagation:** permanent subscriber on each parameter (wired once during parameter discovery). Reads `m_ParentChildLinks` (rebuilt on mode change). Does not filter by origin — children follow regardless of how the parent changed. When a parent is dragged (origin `Handle`), the child is set via `Value +=` which fires the child's `OnChanged` with origin `Code`. The child's reverse-sync sees `Code` and repositions the child handle — origin naturally decays across the parent-child boundary.

Spec compute delegates and resolvers run on the main thread (`NT_BaseToolSystem` is managed). Generator geometry stays in Burst-compiled jobs.

---

## 3. Handle Spec Types

```csharp
public interface IHandleSpec {
    HandleTypeFlags         TypeFlags    { get; }
    string                  Parent       { get; }   // nameof(otherParameter); null = root
    NT_HandleConstraints?   Constraints  { get; }
    float                   Radius       { get; }

    void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param);
}

public interface IHandleSpec<T> : IHandleSpec {
    // Optional. Null = direct value read/write.
    ComputePositionDelegate<T>     ComputePosition     { get; }
    ComputeFromPositionDelegate<T> ComputeFromPosition { get; }
}

public delegate float3 ComputePositionDelegate<T>(NT_BaseToolSystem tool, T value);
public delegate T      ComputeFromPositionDelegate<T>(NT_BaseToolSystem tool, float3 worldPos);
```

| Spec | Parameter type | Notes |
| --- | --- | --- |
| `PositionHandle` | `Float3Parameter` | Free or axis/plane locked via `Axis` / `Plane`. Set `Style = HandleTypeFlags.BezierControlPoint` for the bezier-control visual — overlay renderer keys off the flag. |
| `RotationHandle` | `Float3Parameter` (direction) | Center anchored via `Parent`; plane defined by `Normal` + `ReferenceDirection`. Ships with built-in compute delegates (see below). |
| `CircleHandle` | `FloatParameter` (radius) | Center anchored via `Parent`. Ships with built-in compute delegates (see below). |
| `ComputedPositionHandle` | `FloatParameter` | Requires both compute delegates; renders as position, stores scalar. |

The `IHandleSpec<T>` generic parameter enforces that `Float3Parameter` only accepts `IHandleSpec<float3>[]` and `FloatParameter` only `IHandleSpec<float>[]` — type mismatches are compile errors. `nameof(...)` references are rename-aware (IDE rename refactors carry through) but resolve at tool activation, not compile.

### Built-in compute delegates

`CircleHandle` and `RotationHandle` ship with default `ComputeFromPosition` and `ComputePosition` implementations derived from their declared `Parent`, `Normal`, and `ReferenceDirection` fields. Authors don't need to write compute delegates for standard circle/rotation behavior — declaring `Parent` is sufficient.

`CircleHandle` default: computes radius as distance from the resolved parent center to the drag position, projected onto the handle plane.

`RotationHandle` default: projects the drag position onto the plane defined by `Normal` and `ReferenceDirection` at the parent center, computes the angle, and returns the corresponding direction vector.

Custom delegates still override the defaults for non-standard geometry.

**Resolution caching:** The built-in delegates need the parent parameter's value at drag time. Rather than resolving the `Parent` string on every tick, `RebuildHandlesForActiveMode` caches the resolved `Float3Parameter` reference on the spec instance. The built-in delegates close over this cached reference, turning parent lookup into a direct field read on the drag hot path. Because specs use `init`-only *public* properties but the cached reference is internal mutable state, it lives in a separate `internal Float3Parameter ResolvedParent` field set by the build step — the spec's public API remains immutable.

**Build-time validation:** `RebuildHandlesForActiveMode` asserts that every `FloatParameter`-bound spec (`CircleHandle`, `RotationHandle`, `ComputedPositionHandle`) has non-null `ComputeFromPosition` after defaults are applied. A `FloatParameter` handle with no compute delegate is always a bug — the assertion catches it at build time rather than silently doing nothing on drag.

Concrete spec types use `init`-only properties — specs are immutable after construction. They're shared across rebuilds; mutation would corrupt all modes that reference the same spec. `init` enforces this at compile time.

### Resolver fields

`Parent` is the handle's anchor parameter — the float3 that determines where the handle sits in world space. It serves all anchor roles: root position, circle/rotation center, parent-drag propagation source. Two extra resolvers cover non-positional anchoring:

| Field | Used by | Default |
| --- | --- | --- |
| `Parent` | anchor point (root position, circle/rotation center, parent-drag source) | null (anchor = parameter's own value) |
| `ReferenceDirectionFrom` | `RotationHandle` zero-angle direction | inline `ReferenceDirection` literal |
| `NormalFrom` | `CircleHandle` / `RotationHandle` plane normal | inline `Normal` literal or Y-up |

Resolvers fire at build time (during `RebuildHandlesForActiveMode`). Mode changes trigger a full rebuild, so runtime re-resolution isn't needed. For one-off compositions that don't fit the resolver model, use `ComputePosition` / `ComputeFromPosition` delegates.

---

## 4. Parameter Integration

```csharp
public enum ChangeOrigin { Code, Handle }

public abstract class ParameterBase {
    public event Action<ChangeOrigin> OnChanged;
    // ...existing fields...
}

public abstract class Parameter<T> : ParameterBase {
    public IHandleSpec<T>[] Handles { get; init; }    // null = no spatial handle

    public T Value {
        get => m_Value;
        set => SetValue(value, ChangeOrigin.Code);
    }

    public void SetValue(T value, ChangeOrigin origin) {
        if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
        m_Value = value;
        OnChanged?.Invoke(origin);
    }

}
```

Handles inherit their owning parameter's `Modes` bitflag — when the parameter is hidden in the active mode, its handles aren't built.

### Examples

```csharp
public Float3Parameter StartPosition = new("connect.startPosition", default) {
    Handles = new IHandleSpec<float3>[] { new PositionHandle() }
};

public Float3Parameter CurveStartControlPointPosition = new(
    "connect.curveStartControlPoint", default,
    modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve) {
    Handles = new IHandleSpec<float3>[] {
        new PositionHandle {
            Style  = HandleTypeFlags.BezierControlPoint,
            Parent = nameof(StartPosition)
        }
    }
};

// CircleHandle ships with built-in ComputeFromPosition (radius from center distance)
// and ComputePosition (point on circle). No delegates needed for standard behavior.
public FloatParameter LoopRadius = new(
    "connect.loopRadius", 50f, 1f, 500f, modes: (int)ConnectMode.Loop) {
    Handles = new IHandleSpec<float>[] {
        new CircleHandle { Parent = nameof(LoopControlPointPosition) }
    }
};

public FloatParameter EaseInLength = new(
    "roadShape.easeInLength", 0.3f, 0f, 1f,
    modes: (int)ShapeMode.SlopeEaseInOut | (int)ShapeMode.CurveSmooth) {
    Handles = new IHandleSpec<float>[] {
        new ComputedPositionHandle {
            Constraints = NT_HandleConstraints.AxisXZ,
            ComputePosition = (tool, value) => {
                var t = (NT_RoadShapeToolSystem)tool;
                return math.lerp(t.StartPosition.Value, t.EndPosition.Value, value);
            },
            ComputeFromPosition = (tool, pos) => {
                var t    = (NT_RoadShapeToolSystem)tool;
                var path = t.EndPosition.Value.xz - t.StartPosition.Value.xz;
                var len  = math.length(path);
                if (len < 0.001f) return t.EaseInLength.Min;
                var axis   = path / len;
                var offset = pos.xz - t.StartPosition.Value.xz;
                return math.clamp(math.dot(offset, axis) / len, t.EaseInLength.Min, t.EaseInLength.Max);
            }
        }
    }
};
```

---

## 5. Dispatch & Lifecycle

### Build

```csharp
private readonly struct HandleEntry {
    public ParameterBase Parameter { get; }
    public IHandleSpec   Spec      { get; }
}

private Dictionary<Entity, HandleEntry>              m_HandleEntries;
private Dictionary<ParameterBase, List<Entity>>      m_ParameterHandles;    // reverse lookup
private Dictionary<Float3Parameter, ParentChildLink[]> m_ParentChildLinks;

private class ParentChildLink {
    public Float3Parameter Child;
    public float3          LastParentPos;
}

protected void RebuildHandlesForActiveMode() {
    CancelHandleInteraction();
    DisposeHandles();                   // clears entities + all three lookups

    var active = GetActiveModeFlag();
    foreach (var param in Parameters) {
        if (param.Handles == null) continue;
        if (!IsModeVisible(param.Modes, active)) continue;

        foreach (IHandleSpec spec in param.Handles) {
            var pos    = ResolveInitialPosition(param, spec);
            var entity = CreateHandleEntity(spec, pos);
            m_HandleEntries[entity] = new HandleEntry(param, spec);
            m_ParameterHandles.GetOrAdd(param).Add(entity);
        }
    }

    ResolveParentLinks();               // nameof(...) refs -> parent Entity refs
    BuildParentChildLinks();            // populate m_ParentChildLinks from resolved parent refs
}
```

No subscriptions are wired here — reverse-sync and parent-child subscribers are permanent (wired once during parameter discovery, see §6). The rebuild step only repopulates the data structures those subscribers read.

Mode change triggers rebuild via the mode parameter's `OnChanged` event. `CancelHandleInteraction` defends against rebuild firing mid-drag.

### Drag dispatch

```csharp
void DispatchDrag(Entity handle, float3 position) {
    var (param, spec) = m_HandleEntries[handle];

    switch (param) {
        case Float3Parameter f3p: {
            var s = (IHandleSpec<float3>)spec;
            var v = s.ComputeFromPosition?.Invoke(this, position) ?? position;
            f3p.SetValue(v, ChangeOrigin.Handle);
            break;
        }
        case FloatParameter fp: {
            var s = (IHandleSpec<float>)spec;
            // ComputeFromPosition is guaranteed non-null for FloatParameter specs
            // (built-in defaults for CircleHandle/RotationHandle, required for ComputedPositionHandle;
            //  validated at build time by RebuildHandlesForActiveMode).
            fp.SetValue(s.ComputeFromPosition(this, position), ChangeOrigin.Handle);
            break;
        }
    }
}
```

`SetValue` with `ChangeOrigin.Handle` fires `OnChanged` normally. No explicit `NotifyBindingForParameter` or `m_UpdateNeeded = true` — all subscribers react via `OnChanged`. Reverse-sync skips `Handle` origin (the handle is already at the drag position); parent-child, bindings, and `m_UpdateNeeded` fire as usual. See §6 for subscriber details.

### Reverse-sync

Wired once per parameter during parameter discovery (permanent, tool-lifetime). Reads `m_ParameterHandles` which is rebuilt on mode change — when the parameter has no active handles, the lookup returns nothing and the subscriber is a no-op.

```csharp
param.OnChanged += (origin) => {
    if (origin == ChangeOrigin.Handle) return;
    if (!m_ParameterHandles.TryGetValue(param, out var entities)) return;
    foreach (var entity in entities)
        m_HandleEntries[entity].Spec.SyncToEntity(this, entity, param);
};
```

Each spec type implements `SyncToEntity` with the one update it needs — no external switch:

- `PositionHandle`: `SetHandlePosition(entity, param.Value)`
- `ComputedPositionHandle`: `SetHandlePosition(entity, ComputePosition(tool, param.Value))`
- `CircleHandle`: `SetHandleCircleRadius(entity, param.Value)`
- `RotationHandle`: `SetHandleRotationDirection(entity, param.Value)`

CircleHandle and RotationHandle *positions* are determined by their `Parent` parameter, not by the parameter they're bound to. Position updates for these handles are handled by parent-child propagation (§6.2), not by reverse-sync.

The `ChangeOrigin.Handle` check eliminates bounce: during handle drag, the handle is already at the correct position, so reverse-sync is redundant. For `Code` origin (sliders, `ResetToDefault`, programmatic mutation), reverse-sync runs and updates the handle's visual state.

### Bulk seeding

Tools seed parameters in bulk inside `InitializeConfig` using the regular `Value =` setter. All permanent subscribers are inert during seeding — `m_ParameterHandles` and `m_ParentChildLinks` are empty until `RebuildHandlesForActiveMode` runs, so reverse-sync and parent-child return immediately. `m_UpdateNeeded` is just a flag.

One rule: seeding must precede `RebuildHandlesForActiveMode`, so that `m_ParentChildLinks` entries are initialized with `LastParentPos` reflecting the freshly-seeded values.

---

## 6. Permanent Subscribers

All three subscribers below are wired once per parameter during parameter discovery and never detached. They read mutable lookup structures (`m_ParameterHandles`, `m_ParentChildLinks`) that are rebuilt on mode change — no subscription cleanup needed.

### 6.1 Auto-wire `m_UpdateNeeded`

Replaces the manual `OnChanged += () => m_UpdateNeeded = true;` line every tool's `OnCreate` writes for every parameter:

```csharp
// BaseToolSystem.Parameters.cs — extend the discovery step
foreach (var p in Parameters) {
    p.OnChanged += MarkUpdateNeeded;
}

protected void MarkUpdateNeeded(ChangeOrigin _) => m_UpdateNeeded = true;
```

Tools that need additional per-parameter side-effects still subscribe manually.

### 6.2 Parent → child propagation via subscription

Replaces today's imperative `PropagateToChildren` + O(n²) `SyncChildParameterPositions` per drag tick ([BaseToolSystem.Handles.cs:430-453, 808-825](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:430)) with a permanent subscriber wired during parameter discovery. Like reverse-sync, it reads a lookup structure (`m_ParentChildLinks`) that is rebuilt on mode change:

```csharp
// Wired once during parameter discovery (permanent):
param.OnChanged += (origin) => {
    if (param is not Float3Parameter pp) return;
    if (!m_ParentChildLinks.TryGetValue(pp, out var links)) return;

    foreach (var link in links) {
        var delta = pp.Value - link.LastParentPos;
        link.LastParentPos = pp.Value;
        if (math.lengthsq(delta) < 1e-8f) continue;

        link.Child.Value += delta;
        // Child's Value setter fires OnChanged(Code). The child's reverse-sync
        // sees Code origin, runs, and repositions the child handle entity.
    }
};
```

`m_ParentChildLinks` is populated by `BuildParentChildLinks()` at the end of `RebuildHandlesForActiveMode`. Each `ParentChildLink` holds the child `Float3Parameter` and a mutable `LastParentPos`, initialized to the parent's current value at rebuild time. When the parent has no active children, the lookup returns nothing and the subscriber is a no-op.

Does not filter by origin — children follow regardless of how the parent changed. When a parent is dragged (`Handle` origin), `child.Value += delta` fires the child's `OnChanged` with origin `Code`. The origin naturally decays from `Handle` to `Code` across the parent-child boundary, which is correct: the child's handle *wasn't* directly dragged and *does* need repositioning by reverse-sync.

Per-drag-tick cost drops from O(parents × all_handles) to O(direct_children).

---

## 7. Migration Plan

Phase 0 (scaffolding) + Phase 1 split into two sub-phases by tool. Phase 1a migrates infrastructure and Connect (the most complex tool); Phase 1b migrates the remaining tools and deletes old code. Old dispatch temporarily coexists during 1a via fallthrough.

### Phase 0 — Scaffolding

Mechanical, no behavior change.

- Add `ChangeOrigin` enum. Migrate `ParameterBase.OnChanged` from `Action` to `Action<ChangeOrigin>`. Add `Parameter<T>.SetValue(T, ChangeOrigin)`. Update existing subscribers to accept the new signature (mechanical — add `_` discard or use `ChangeOrigin` where needed).
- **§6.1** Auto-wire `MarkUpdateNeeded` in `BaseToolSystem.Parameters.cs`. Remove the manual lines from every tool's `OnCreate`.
- Add `IHandleSpec`, `IHandleSpec<T>`, the four concrete spec types, and the `Handles` property on `Parameter<T>`.
- Add lookup structures on `BaseToolSystem`: `m_ParameterHandles`, `m_ParentChildLinks`. Wire permanent reverse-sync and parent-child subscribers during parameter discovery (alongside `MarkUpdateNeeded`).

### Phase 1a — Infrastructure + Connect

Connect is the most complex tool (position, circle, rotation handles; parent-child; three modes). Migrating it first validates the full design before touching other tools.

**New dispatch infrastructure** (`BaseToolSystem.Handles.cs`):
- `RebuildHandlesForActiveMode` walking `Parameters[].Handles` inline, populating `m_HandleEntries`, `m_ParameterHandles`, `m_ParentChildLinks`.
- `ResolveParentLinks` (`nameof` → `Entity`).
- `BuildParentChildLinks` (populate `m_ParentChildLinks` from resolved parent refs).
- Generic `DispatchDrag` using `SetValue(v, ChangeOrigin.Handle)`.
- `HandleConstraints` static helpers (`AxisXZ`, `Plane(normal)`, `LockY`).

**Connect migration:**
- Inline handles for SimpleCurve, Loop, ComplexCurve on parameter declarations.
- Loop's rotation handles validate `Parent` + `ReferenceDirectionFrom`.
- Delete `Connect.Handles.cs`, `IHandleableConnectionGenerator`.
- Drop `BuildHandleDefinitions` from Connect's generators.

**Old dispatch remains** for RoadShape and Generate during this phase — `DispatchDrag` falls through to the existing virtuals for handles not found in `m_HandleEntries`.

**Verification (1a):**
- Connect: SimpleCurve → Loop → ComplexCurve mode switching, drag, slider sync, reset, parent-drag-moves-children.
- Bounce: verify no double-fire on handle drag (ChangeOrigin.Handle skips reverse-sync).
- RoadShape and Generate: unchanged behavior (still using old dispatch path).

### Phase 1b — Remaining tools + cleanup

**Tool migrations:**
- **RoadShape.** `ComputedPositionHandle`s for `EaseInLength` / `EaseOutLength` / etc. with projection delegates.
- **Generate.** Inline handles for Grid mode. Circle has none.
- **Parallel.** Slider-only; no handles.

**Deletions** (all in this phase, after all tools are migrated):
- `TransformHandleDefinition` struct.
- Integer `Key` / `ParentKey` machinery and the two-pass `CreateHandlesFromDefinitions`.
- Per-type drag virtuals: `OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged`, `OnParameterHandleDragged`.
- `BuildHandleDefinitions` on remaining generators and transforms.
- `Generate.Handles.cs`, `RoadShapeToolSystem.Handles.cs`.
- `IHandleableGenerator`, `IHandleableTransformation` interfaces.
- `PropagateToChildren`, `SyncChildParameterPositions`.
- Old dispatch fallthrough path added in Phase 1a.

**Verification (1b):**
- RoadShape: ease-length drag (computed path); template presets (`SlopeLinear()`, `CurveSmooth()`, etc.) firing `OnChanged` so reverse-sync repositions handles.
- Generate: Grid drag.
- Subscriber stability: cycle modes 50× across all tools; `Parameter.OnChanged` subscriber count is constant (permanent subscribers, no per-rebuild wiring).
- Overlay rendering unchanged (entities still carry `NT_Handle*` components, including the `BezierControlPoint` flag now driven by `PositionHandle.Style`).

---

## 8. File Reference

| Phase | Action | Files |
| --- | --- | --- |
| 0 | New | `Systems/Handles/IHandleSpec.cs`, `PositionHandle.cs`, `CircleHandle.cs`, `RotationHandle.cs`, `ComputedPositionHandle.cs` |
| 0 | New | `Systems/Tools/Parameters/ChangeOrigin.cs` |
| 0 | Modify | `Systems/Tools/Parameters/ParameterBase.cs` (`OnChanged` signature → `Action<ChangeOrigin>`) |
| 0 | Modify | `Systems/Tools/Parameters/Parameter.cs` (`Handles`, `SetValue(T, ChangeOrigin)`) |
| 0 | Modify | `Systems/Tools/Base/BaseToolSystem.Parameters.cs` (§6.1, permanent reverse-sync + parent-child subscribers) |
| 1a | New | `Systems/Handles/HandleConstraints.cs` (static helpers) |
| 1a | Modify | `Systems/Tools/Base/BaseToolSystem.Handles.cs` (new dispatch + old fallthrough) |
| 1a | Modify | `Systems/Tools/Connect/NT_ConnectToolSystem.cs` (handle declarations) |
| 1a | Modify | `Systems/Tools/Connect/Generators/*.cs` (drop `BuildHandleDefinitions`) |
| 1a | Delete | `Systems/Tools/Connect/Connect.Handles.cs` |
| 1a | Delete | `IHandleableConnectionGenerator` |
| 1b | Modify | `Systems/Tools/{RoadShape,Generate}/NT_*ToolSystem.cs` (handle declarations) |
| 1b | Modify | `Systems/Tools/{RoadShape,Generate}/{Generators,Transforms}/*.cs` (drop `BuildHandleDefinitions`) |
| 1b | Delete | `Systems/Tools/{RoadShape,Generate}/*.Handles.cs` |
| 1b | Delete | `Systems/Tools/Base/TransformHandleDefinition.cs` |
| 1b | Delete | `IHandleableTransformation`, `IHandleableGenerator` |
| 1b | Delete | Old dispatch fallthrough, `PropagateToChildren`, `SyncChildParameterPositions` |
