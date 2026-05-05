# Handle System Refactor (v2)

Design document for nesting handle declarations under parameters, eliminating per-tool `BuildHandleDefinitions` methods, the string-keyed parameter lookup, and the residual dispatch patterns that remain after the parameter refactor. Written 2026-05-04.

This document supersedes earlier handle-refactor sketches in this directory.

---

## 1. Context

### Current architecture (post-parameter-refactor)

The parameter system refactor (see `parameter-system-refactor.md`) eliminated `HandleKeys` and gave handle definitions a direct `ParameterBase` reference. What remains is a two-layer authoring pattern:

1. Parameters declared inline on the tool system as fields (Phases 1–3 of the parameter refactor).
2. Handles declared in generators via static `BuildHandleDefinitions(in JobConfig, IReadOnlyDictionary<string, ParameterBase>)` methods, with parameter references resolved by string key.

Relevant files:
- `Systems/Tools/Base/TransformHandleDefinition.cs` — definition struct
- `Systems/Tools/Base/BaseToolSystem.Handles.cs` — lifecycle, raycasting, drag dispatch
- `Systems/Tools/<Tool>/Generators/*.cs` — per-mode generators with `BuildHandleDefinitions`
- `Systems/Tools/RoadShape/RoadShapeToolSystem.Handles.cs` — override-and-compare drag callback

### Pain points

1. **Stringly-typed parameter lookup.** Generators reference parameters via `parameters["connect.curveStartControlPointPosition"]`. Drift-prone, no compile-time check, redundant given the tool already holds the parameter as a typed field.
2. **Generators author handles.** Each mode-specific generator has a `BuildHandleDefinitions` static method. Handles aren't geometry — they're spatial views over parameters — but they live alongside actual job-side geometry.
3. **Override-and-compare dispatch.** RoadShape overrides `OnParameterHandleDragged` and uses `if (param == EaseInLength) ... else if (param == EaseOutLength)` to route by object identity. Same shape as the `HandleKeys` switch the parameter refactor killed, just with a different key type.
4. **Integer parent keys.** `TransformHandleDefinition.ParentKey` is an arbitrary `int` constant declared at the top of each generator (`const int keyStartCtl = 2;`). Bookkeeping with no semantic value at the call site.
5. **No co-location of parameter and handle metadata.** A reader bounces between the tool system file and the generator file to understand what a parameter looks like in the world.

### What is *not* a problem (do not change)

- **Mode dispatch in jobs.** Burst forbids virtual dispatch. The `switch (Mode)` in jobs is mandatory. Stays.
- **Generator geometry.** `GenerateConnection` / `Process` / `GetCurves` own the actual algorithm. Untouched.
- **ECS entity model for handles.** Spawning entities with `NT_Handle`, `NT_HandlePosition`, `NT_HandleCircle`, `NT_HandleConstraints`, etc. is load-bearing for raycasting and rendering. Untouched.
- **Raycast hit detection.** `GetClosestHandleFromRay` and the drag input state machine in `BaseToolSystem.Handles.cs` stay; only the *dispatch* layer above them changes.

---

## 2. Goals

- Declare each handle once, **next to the parameter it drives**, with all spatial metadata co-located.
- One pattern across all tools — same authoring shape whether the tool has modes (Connect, Generate, RoadShape) or not (Parallel).
- Eliminate string-keyed parameter lookup; handle declarations reference parameters by typed field via `nameof`.
- Eliminate generators' `BuildHandleDefinitions` methods; generators retain only job-side geometry.
- Mode-filter handles via the same `Modes` bitflag used for parameters.
- Replace override-and-compare drag dispatch with declarative delegates on the handle spec.
- Support multiple handles per parameter without bloating the single-handle simple case.

### Non-goals

- Changing how generators dispatch on mode in jobs.
- Replacing the ECS-based handle entity model.
- Replacing raycast hit detection or render systems.
- Changing the parameter system itself (additive only — `Handles` is a new property on `Parameter<T>`).

---

## 3. Architecture Overview

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

**Discovery flow:**
1. `NT_BaseToolSystem` reflects parameter fields once (already done).
2. On tool activation / mode change, the base system walks `Parameters[].Handles` inline, filters by active mode, and materializes ECS handle entities.
3. Each entity registers in a single `m_HandleEntries` map keyed on `Entity` and holding the owning parameter + spec.

**Drag flow (single generic path):**
1. Raycast hits an entity (existing).
2. Base system reads `m_HandleEntries[entity]`.
3. If spec has `ComputeFromPosition`, call it and write result to parameter via `SetWithoutNotify`; otherwise write directly via `SetWithoutNotify`.
4. Pulse bindings + `m_UpdateNeeded` (see §7 — bounce prevention).
5. Sibling handles re-position via `ComputePosition` only when `OnChanged` fires from non-handle sources (sliders, reset).

**What goes away:**
- `BuildHandleDefinitions` methods on generators.
- `IReadOnlyDictionary<string, ParameterBase>` parameter lookup.
- `TransformHandleDefinition.Key` / `ParentKey` integer machinery (subsumed by name-based parent resolution).
- `RoadShapeToolSystem.Handles.cs` override partial.
- Per-type drag virtuals (`OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged`, `OnParameterHandleDragged`).

**What stays (intentionally):**
- All `NT_Handle*` ECS components.
- Raycast hit detection in `BaseToolSystem.Handles.cs`.
- The drag input state machine (Idle → PendingAction → Dragging).
- Generator geometry methods.

---

## 4. Handle Spec Types

### Interface

```csharp
public interface IHandleSpec {
    HandleTypeFlags         TypeFlags    { get; }
    string                  Parent       { get; }   // nameof(otherParameter); null = root
    NT_HandleConstraints?   Constraints  { get; }
    float                   Radius       { get; }
}

public interface IHandleSpec<T> : IHandleSpec {
    // Optional value derivation for computed handles.
    // When null, the base system reads/writes the parameter value directly.
    ComputePositionDelegate<T>     ComputePosition     { get; }
    ComputeFromPositionDelegate<T> ComputeFromPosition { get; }
}

public delegate float3 ComputePositionDelegate<T>(NT_BaseToolSystem tool, T value);
public delegate T      ComputeFromPositionDelegate<T>(NT_BaseToolSystem tool, float3 worldPos);
```

### Built-in specs

| Spec | Parameter type | TypeFlags | Notes |
| --- | --- | --- | --- |
| `PositionHandle` | `Float3Parameter` | `Position` (default) or `BezierControlPoint` (via `Style`) | Free or axis/plane locked via `Axis` / `Plane`; bezier-control visual selected by `Style = BezierControlPoint` — overlay renderer keys off the flag |
| `RotationHandle` | `Float3Parameter` (direction) | `Rotation` | Center anchored via `Parent`; plane defined by `Normal` + `ReferenceDirection` |
| `CircleHandle` | `FloatParameter` (radius) | `Circle` | Center anchored via `Parent` (same field as parent-drag linkage) |
| `ComputedPositionHandle` | `FloatParameter` | `Position` | Requires both compute delegates; renders as position, stores scalar |

Type discipline is enforced by the `IHandleSpec<T>` generic parameter — a `Float3Parameter` can only hold `IHandleSpec<float3>[]`, a `FloatParameter` only `IHandleSpec<float>[]`. Mismatches are compile errors.

`Parent` and the other sibling references are `nameof(...)` strings resolved at build time. That's rename-aware (IDE rename refactors carry through; typos surface as build-time discovery failures), but it is *not* compile-checked the way the generic type parameter is. A misnamed sibling field rename outside an IDE will fail at tool activation, not at compile.

### Sibling parameter references

`Parent` does double duty: it's the handle's *anchor parameter* — the float3 that determines where the handle sits in world space. For a position-style handle, the anchor is the handle's own initial position and parent-drag propagation moves the child by the parent's delta. For a circle or rotation handle, the anchor *is* the geometric center: there's no separate "follow this when it moves" concept because the center already moves with the parent by construction. One field, one mental model.

What `Parent` *doesn't* express is non-positional anchoring — directions and plane normals. Loop's rotation handle has its center anchored at `StartPosition` (handled by `Parent`) but its zero-angle direction comes from `StartDirection`, which is a different parameter and a different role. Hence two extra resolver fields:

| Field | Type | Used by | Default |
| --- | --- | --- | --- |
| `Parent` | `string` (nameof) | anchor point for *any* handle (root position, circle/rotation center, parent-drag propagation source) | null (root handle, anchor = parameter's own value) |
| `ReferenceDirectionFrom` | `string` (nameof) | `RotationHandle` zero-angle direction | inline `ReferenceDirection` literal |
| `NormalFrom` | `string` (nameof) | `CircleHandle` / `RotationHandle` plane normal | inline `Normal` literal or Y-up |

Each resolver fires once at build time (initial value) and re-fires when the referenced parameter's `OnChanged` triggers, so changing `StartDirection` rotates the handle's plane without rebuilding the entity. The §7 reverse-sync subscription handles both kinds of update through the same mechanism — see §7.

For one-off compositions that don't fit the resolver shape, use `ComputePosition` / `ComputeFromPosition` delegates instead.

### Dispatch threading boundary

Spec compute delegates and resolvers run on the main thread inside `BaseToolSystem`. `NT_BaseToolSystem` is a managed type, so the delegates are not Burst-compatible by design — they don't need to be. Generator geometry (`GenerateConnection`, `Process`, `GetCurves`) keeps running in Burst-compiled jobs as before; only the dispatch layer above it is plain C#.

---

## 5. Parameter Integration

`Handles` is added to the generic parameter base, defaulting to null:

```csharp
public abstract class Parameter<T> : ParameterBase {
    public IHandleSpec<T>[] Handles { get; init; }    // null = no spatial handle
    // ...existing fields...
}
```

### Declaration examples

#### Slider-only (Parallel)

No `Handles` initializer — Parallel is binding-only.

```csharp
public FloatParameter HorizontalOffset = new("parallel.horizontalOffset", 20f, 0f, 80f);
```

#### Single handle, mode-filtered (Connect)

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

public FloatParameter LoopRadius = new(
    "connect.loopRadius", 50f, 1f, 500f,
    modes: (int)ConnectMode.Loop) {
    Handles = new IHandleSpec<float>[] {
        new CircleHandle { Parent = nameof(StartPosition) }
    }
};
```

#### Computed handle (RoadShape)

```csharp
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
                var t = (NT_RoadShapeToolSystem)tool;
                var path = t.EndPosition.Value.xz - t.StartPosition.Value.xz;
                var len = math.length(path);
                if (len < 0.001f) return t.EaseInLength.Min;
                var axis = path / len;
                var offset = pos.xz - t.StartPosition.Value.xz;
                return math.clamp(math.dot(offset, axis) / len, t.EaseInLength.Min, t.EaseInLength.Max);
            }
        }
    }
};
```

### Mode filtering

Handles inherit their owning parameter's `Modes` bitflag. When the parameter is hidden in the active mode, its handles aren't built. No spec-level override — if a real case ever needs finer control, add it then.

### Parent reference

`Parent = nameof(OtherParameter)` resolves at build time to the root handle entity of `OtherParameter`. See Open Q #4 for the multi-handle case.

---

## 6. Generator Role (Reduced)

Before:
```csharp
public struct SimpleCurveGenerator : IConnectionGenerator {
    public void GenerateConnection(in ConnectJobConfig config, ref NativeList<CurveDef> curves) { ... }

    // REMOVED in this refactor:
    public static TransformHandleDefinition[] BuildHandleDefinitions(
        in ConnectJobConfig config,
        IReadOnlyDictionary<string, ParameterBase> parameters) { ... }
}
```

After:
```csharp
public struct SimpleCurveGenerator : IConnectionGenerator {
    public void GenerateConnection(in ConnectJobConfig config, ref NativeList<CurveDef> curves) { ... }
    // Handles now live on the parameters themselves.
}
```

Generators stay structs (Burst-compatible), keep their mode-dispatch role, lose handle authoring entirely. The `IHandleableConnectionGenerator`, `IHandleableGenerator`, and `IHandleableTransformation` interfaces can be deleted in cleanup.

---

## 7. Discovery & Lifecycle

### Build (mode change or tool activation)

A single map keyed by handle entity holds both the owning parameter and its spec:

```csharp
private readonly struct HandleEntry {
    public ParameterBase Parameter { get; }
    public IHandleSpec   Spec      { get; }
}

private Dictionary<Entity, HandleEntry> m_HandleEntries;

protected void RebuildHandlesForActiveMode() {
    CancelHandleInteraction();   // §7 — mid-drag rebuild
    DisposeHandles();

    var active = GetActiveModeFlag();
    foreach (var param in Parameters) {
        if (param.Handles == null) continue;
        if (!IsModeVisible(param.Modes, active)) continue;

        foreach (IHandleSpec spec in param.Handles) {
            var pos    = ResolveInitialPosition(param, spec);
            var entity = CreateHandleEntity(spec, pos);
            m_HandleEntries[entity] = new HandleEntry(param, spec);
        }
    }

    ResolveParentLinks();   // nameof(...) refs -> parent Entity refs
}
```

No `HandleSchema` layer — parameters are already discovered and cached, so walking `Parameters[].Handles` inline is two lines and keeps the spec/parameter relationship visible at the call site. Mode change triggers rebuild via the mode parameter's `OnChanged` event.

### Drag dispatch (replaces all per-type virtuals)

```csharp
void DispatchDrag(Entity handle, float3 position) {
    var (param, spec) = m_HandleEntries[handle];

    switch (param) {
        case Float3Parameter f3p: {
            var s = (IHandleSpec<float3>)spec;
            var v = s.ComputeFromPosition?.Invoke(this, position) ?? position;
            f3p.SetWithoutNotify(v);
            break;
        }
        case FloatParameter fp: {
            var s = (IHandleSpec<float>)spec;
            var v = s.ComputeFromPosition?.Invoke(this, position) ?? GetHandleScalarValue(handle);
            fp.SetWithoutNotify(v);
            break;
        }
    }

    NotifyBindingForParameter(param);
    m_UpdateNeeded = true;
}
```

`OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged`, `OnParameterHandleDragged` virtuals all delete. RoadShape's `Handles.cs` partial deletes.

### Parameter → handle position sync

When a parameter changes from a non-handle source (slider, reset, external mutation):

```csharp
// Wired once per parameter-with-handles in RebuildHandlesForActiveMode.
// `ComputePosition` lives on IHandleSpec<T>, so the dispatch switches on
// parameter type to cast the spec correctly — same shape as DispatchDrag above.
param.OnChanged += () => {
    foreach (var entity in HandlesFor(param)) {
        var spec = m_HandleEntries[entity].Spec;
        float3 pos;
        switch (param) {
            case Float3Parameter f3p:
                var s3 = (IHandleSpec<float3>)spec;
                pos = s3.ComputePosition?.Invoke(this, f3p.Value) ?? f3p.Value;
                break;
            case FloatParameter fp:
                var sf = (IHandleSpec<float>)spec;
                pos = sf.ComputePosition?.Invoke(this, fp.Value) ?? DefaultPositionFor(param, spec);
                break;
            default: continue;
        }
        SetHandlePosition(entity, pos);
    }
};
```

#### Bounce prevention

`Parameter<T>.Value` already short-circuits on equality, so the loop `handle drag → param.Value = ... → OnChanged → SetHandlePosition` won't bounce as long as `ComputePosition(ComputeFromPosition(p)) ≈ p`. Float drift in computed-handle projection math breaks that — `OnChanged` fires twice per drag tick when `ComputeFromPosition(ComputePosition(v)) ≠ v` exactly.

`OnChanged` carries no provenance, so a reverse-sync subscriber can't filter handle-driven changes by inspection. The mitigation is to extend `Parameter<T>` with a silent writer:

```csharp
public void SetWithoutNotify(T value) {
    if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
    m_Value = value;
}
```

Drag dispatch (shown above) uses `SetWithoutNotify` and pulses the UI binding via `NotifyBindingForParameter` — a thin helper that pokes the Colossal binding bridge for `param` directly. Bindings stay in sync with the dragged value, but the reverse-sync subscriber attached to `OnChanged` doesn't fire, so the bounce loop is broken at the source rather than papered over with a flag.

This also resolves the §8.2 parent-child case: parent-drag writes the parent param via `SetWithoutNotify`, then explicitly calls `childParam.SetWithoutNotify(child + delta)` and pulses bindings — no reverse-sync fights the imperative child write.

UI sliders, `ResetToDefault`, and any external mutation continue to use `Value =` and fire `OnChanged` normally — that's the only path the reverse-sync subscriber needs to react to.

### Seeding order

Tools seed parameters in bulk inside `InitializeConfig` (see [ConnectToolSystem.Lifecycle.cs:61-72](NetworkTools.Mod/Systems/Tools/Connect/ConnectToolSystem.Lifecycle.cs:61)). Two ordering rules:

1. **Bulk seeding always precedes `RebuildHandlesForActiveMode`.** Otherwise the parent-child closures in §8.2 capture stale `lastParentPos` values, and the next user drag fires propagation with a startup-sized delta that warps every child.
2. **Bulk seeding uses `SetWithoutNotify`.** Each `Param.Value = ...` assignment in `InitializeConfig` becomes `Param.SetWithoutNotify(...)`. The post-seed rebuild creates handles at the freshly-seeded values, so reverse-sync would be wasted work; bindings get refreshed in one batch via `param.ForceNotify()` (or by relying on the binding bridge's own post-rebuild push) once seeding is complete.

The `ResetAll` / `Reset(key)` path on `BaseToolSystem.Parameters.cs` is different — those are user-facing intents that *should* fire `OnChanged` so handles re-position and bindings update. They keep the regular setter; only the bulk-from-generator seeding switches to `SetWithoutNotify`.

### Mid-drag rebuild

`RebuildHandlesForActiveMode` destroys every handle entity. If a drag is in progress (`m_DraggedHandle != Entity.Null`), the held entity becomes invalid mid-state-machine. Today this can't happen because mode change is UI-driven and UI is suppressed during drag, but the rebuild path should call `CancelHandleInteraction` defensively at the start, not rely on call-site discipline.

---

## 8. Adjacent Improvements

Two cleanups that share the "declare once, base wires it via events" theme. They share files with the core refactor and ride along rather than being deferred.

### 8.1 Auto-wire `m_UpdateNeeded` during parameter discovery

Every tool's `OnCreate` currently does:

```csharp
HorizontalOffset.OnChanged    += () => m_UpdateNeeded = true;
VerticalOffset.OnChanged      += () => m_UpdateNeeded = true;
HorizontalDirection.OnChanged += () => m_UpdateNeeded = true;
// ...one line per parameter
```

Discovery already iterates every parameter field. Move the wiring there:

```csharp
// BaseToolSystem.Parameters.cs — extend the discovery step
foreach (var p in Parameters) {
    p.OnChanged += MarkUpdateNeeded;
}

protected void MarkUpdateNeeded() => m_UpdateNeeded = true;
```

Tools that need *additional* side-effects on a specific parameter still subscribe manually; the default just stops being boilerplate.

This is parameter-system cleanup, independent of handles — but lands here because the handle refactor adds a similar discovery pass and the patterns should match.

### 8.2 Parent → child propagation via subscription, not imperative call

Today's `HandlePositionDrag` does:

```csharp
f3p.Value = handlePos;
if (math.lengthsq(delta) > 0f) {
    PropagateToChildren(handle, delta);    // imperative walk
    SyncChildParameterPositions(handle);   // re-scans every handle in the tool to find children of this parent
}
```

`SyncChildParameterPositions` ([BaseToolSystem.Handles.cs:808-825](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:808)) iterates every handle in the tool to find children of one parent. With the new spec/parameter graph, the parent-child relationship is known at discovery — wire it as a subscription at creation time:

```csharp
// In RebuildHandlesForActiveMode, after entities exist:
foreach (var entry in activeEntries) {
    if (entry.Spec.Parent == null) continue;

    var parentParam = ResolveParentParameter(entry.Spec.Parent);
    var childParam  = entry.Parameter as Float3Parameter;
    if (parentParam is not Float3Parameter pp || childParam == null) continue;

    var lastParentPos = pp.Value;
    pp.OnChanged += () => {
        var delta = pp.Value - lastParentPos;
        lastParentPos = pp.Value;
        if (math.lengthsq(delta) < 1e-8f) return;

        childParam.Value += delta;
        // Child's reverse-sync subscription (§7) moves the child handle entity.
        // No explicit position write needed.
    };
}
```

`PropagateToChildren` and `SyncChildParameterPositions` both delete. Per-drag-tick cost drops from O(parents × all_handles) to O(direct_children). The handle-entity update piggybacks on the §7 reverse-sync we already wired for every parameter-bound handle.

This is core to the new pattern, not optional — it eliminates per-tick map lookups during drag and replaces an imperative one-shot call with a subscription that fires only when state actually changes.

---

## 9. Migration Plan

Two phases: scaffolding first (mechanical, no behavior change), then a single one-shot migration that flips every tool to the new dispatch in one go and deletes the old machinery.

No dual-dispatch shim. Three tools is small enough that carrying both paths through intermediate states costs more than it buys — one PR replaces the lot, the deletions land with the migration, and there's no half-migrated state to debug.

### Phase 0 — Scaffolding

Mechanical changes that don't depend on any tool being migrated. Land first to shrink the migration PR.

- **§8.1** Auto-wire `MarkUpdateNeeded` to every discovered parameter in `BaseToolSystem.Parameters.cs`. Remove the manual `OnChanged += () => m_UpdateNeeded = true` lines from every tool's `OnCreate`. No new types, no new dispatch.
- Add `IHandleSpec`, `IHandleSpec<T>`, the concrete spec types (`PositionHandle`, `CircleHandle`, `RotationHandle`, `ComputedPositionHandle`), and the `Handles` property on `Parameter<T>`. No discovery, no dispatch, no migration — just the type plumbing.
- Decide and implement scoped-subscription cleanup (see §10.6) — load-bearing for §7 reverse-sync and §8.2 parent-child. Either an `IDisposable` token list cleared on `DisposeHandles`, or a `Parameter.OnChangedScoped` overload.
- Add `Parameter<T>.SetWithoutNotify` (§7) and a `NotifyBindingForParameter(ParameterBase)` helper on `BaseToolSystem`.

### Phase 1 — One-shot migration

Single PR. Every tool moves to the new dispatch, every old code path deletes.

**New dispatch infrastructure** (in `BaseToolSystem.Handles.cs`):
- `RebuildHandlesForActiveMode` walking `Parameters[].Handles` inline (no `HandleSchema` layer). Calls `CancelHandleInteraction` defensively at the top.
- `ResolveParentLinks` for `nameof(...)` → `Entity` resolution.
- Single `m_HandleEntries` map keyed by `Entity`, holding the owning parameter + spec.
- Generic `DispatchDrag` (§7) using `SetWithoutNotify` + `NotifyBindingForParameter`.
- Reverse-sync subscriptions (§7) wired via the Phase 0 scoped-cleanup mechanism.
- **§8.2** Parent → child propagation as scoped `OnChanged` subscriptions.
- `HandleConstraints` static helpers (`AxisXZ`, `Plane(normal)`, `LockY`).

**Tool migrations** (all three at once):
- **Connect.** Declare handles inline on parameters across all 3 modes (SimpleCurve, Loop, ComplexCurve). Loop's rotation handles validate the `Parent` + `ReferenceDirectionFrom` resolver path.
- **RoadShape.** Declare `ComputedPositionHandle`s for `EaseInLength` / `EaseOutLength` / etc. with projection delegates inline. Validates the compute-delegate path.
- **Generate.** Declare handles inline for Grid mode. Circle mode currently has none.
- **Parallel.** Slider-only; nothing to migrate.

**Bulk-seeding conversion:**
- Every `InitializeConfig` switches the seeding burst to `SetWithoutNotify(...)`, rebuild call immediately after. `ResetAll` / `Reset(key)` keep the regular setter.

**Deletions** (all in this same PR):
- `TransformHandleDefinition` struct.
- Integer `Key` / `ParentKey` machinery and the two-pass entity creation in `CreateHandlesFromDefinitions`.
- Per-type drag virtuals: `OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged`, `OnParameterHandleDragged`.
- `BuildHandleDefinitions` static methods on every generator and transform.
- `Connect.Handles.cs`, `Generate.Handles.cs`, `RoadShapeToolSystem.Handles.cs`.
- `IHandleableConnectionGenerator`, `IHandleableGenerator`, `IHandleableTransformation` interfaces.
- `PropagateToChildren` and `SyncChildParameterPositions` (replaced by §8.2 subscriptions).

**Verification:**
- Connect: SimpleCurve → Loop → ComplexCurve mode switching, handle drag, slider sync, reset, parent-drag-moves-children.
- RoadShape: ease-length handle drag (computed path), template presets (`SlopeLinear()`, `CurveSmooth()`, etc.) firing `OnChanged` so reverse-sync repositions handles.
- Generate: Grid mode handle drag.
- Bounce: Connect's straightforward handles first, then RoadShape's projection math.
- Subscription cleanup: cycle modes 50× across all tools, confirm `Parameter.OnChanged` subscriber count returns to baseline after each rebuild.
- Overlay rendering: unchanged (entities still carry the same `NT_Handle*` components, including the `BezierControlPoint` flag now driven by `PositionHandle.Style`).

---

## 10. Open Questions / Risks / Migration Gotchas

1. **CircleHandle center inference.** Loop mode's circle is anchored at `LoopControlPointPosition` — `Parent = nameof(LoopControlPointPosition)` covers it; no separate center concept needed. For "midpoint of A and B" cases fall back to a `ComputePosition` delegate.

2. **Spec immutability.** Specs are declared as field initializers and the discovery list is cached per concrete tool type. Confirm specs don't mutate at runtime — if any do (e.g., `Constraints` changing per-mode), move that mutation to a per-entity map keyed on `Entity`, not on the shared spec.

3. **Compute delegate allocations.** Lambda captures (`(tool, pos) => ...`) allocate at type init when the field initializer runs. That's once per tool instance, never on the hot path. Static methods or `Func` cache fields are an option if profiling later shows allocation pressure — unlikely.

4. **Parent group semantics for multi-handle parameters.** When `Parent = nameof(StartPosition)` and `StartPosition` has more than one handle, child handles need a clear "follow which parent entity?" answer. Initial choice: rigid group — children move with any parent sub-handle's drag. Today every parent-side parameter has exactly one handle, so the choice is invisible. Keep `Parent` as a single `string` for now, but note that the resolver field shape leaves room for a future `ParentSelector` (e.g., `Parent = (nameof(StartPosition), HandleIndex: 0)`) without breaking existing call sites — the moment a real multi-handle parameter ships, the API needs revisiting before that parameter does.

5. **Handle identity on rebuild.** Mode changes destroy and recreate handle entities. If anything outside the base system holds a handle `Entity` reference (e.g., overlay rendering caches), invalidate on rebuild. Worth a grep during Phase 1.

6. **Subscription cleanup on rebuild — load-bearing, not optional.** §8.2's parent-child subscriptions and §7's reverse-sync subscriptions both attach to `Parameter.OnChanged`. Mode changes rebuild the handle set — old subscriptions must detach or they accumulate per mode-cycle and pin tool state past tool deactivation. Closures here capture parameter references, last-position state, and the spec, so leaks are real GC pressure, not just stale-handler noise. Either track them on a per-rebuild list and detach on `DisposeHandles`, or grow a `Parameter.OnChangedScoped(IDisposable)` variant. **Decide and implement in Phase 0** — both §7 and §8.2 land in Phase 1 and ship broken without this.

7. **TS-binding codegen impact.** The codegen described in `generator-api-and-jobconfig-codegen.md` walks `ParameterBase`-derived fields. Adding `Handles` as an `init`-only property on `Parameter<T>` shouldn't cross the binding boundary (handles aren't bindable, and codegen targets the binding key + value type, not arbitrary properties), but re-run codegen during Phase 0 and diff the output to confirm no spurious entries surface.

---

## 11. Reference: Files Likely To Change

| Area | Files |
| --- | --- |
| New (Phase 0) | `Systems/Handles/IHandleSpec.cs`, `PositionHandle.cs`, `CircleHandle.cs`, `RotationHandle.cs`, `ComputedPositionHandle.cs` |
| New (Phase 1) | `Systems/Handles/HandleConstraints.cs` (static helpers — `AxisXZ`, `Plane(normal)`, `LockY`, etc.) |
| Modified (Phase 0) | `Systems/Tools/Parameters/Parameter.cs` (add `Handles` property + `SetWithoutNotify` to `Parameter<T>`) |
| Modified (Phase 0) | `Systems/Tools/Base/BaseToolSystem.Parameters.cs` (§8.1 auto-wire `MarkUpdateNeeded`; `NotifyBindingForParameter` helper) |
| Modified (Phase 1) | `Systems/Tools/Base/BaseToolSystem.Handles.cs` — generic dispatch, mode-change rebuild, reverse-sync wiring, parent-child subscriptions (§8.2). Deletes per-type drag virtuals and the two-pass `CreateHandlesFromDefinitions` machinery. |
| Modified (Phase 1) | `Systems/Tools/Connect/NT_ConnectToolSystem.cs` — handle declarations on parameters |
| Modified (Phase 1) | `Systems/Tools/Connect/Generators/*.cs` — remove `BuildHandleDefinitions` |
| Modified (Phase 1) | `Systems/Tools/RoadShape/NT_RoadShapeToolSystem.cs` — computed handle declarations |
| Modified (Phase 1) | `Systems/Tools/RoadShape/Transforms/*.cs` — remove `BuildHandleDefinitions` |
| Modified (Phase 1) | `Systems/Tools/Generate/NT_GenerateToolSystem.cs` — handle declarations |
| Modified (Phase 1) | `Systems/Tools/Generate/Generators/*.cs` — remove `BuildHandleDefinitions` |
| Deleted (Phase 1) | `Systems/Tools/Connect/Connect.Handles.cs` |
| Deleted (Phase 1) | `Systems/Tools/RoadShape/RoadShapeToolSystem.Handles.cs` |
| Deleted (Phase 1) | `Systems/Tools/Generate/Generate.Handles.cs` |
| Deleted (Phase 1) | `Systems/Tools/Base/TransformHandleDefinition.cs` |
| Deleted (Phase 1) | `IHandleableConnectionGenerator`, `IHandleableTransformation`, `IHandleableGenerator` interfaces |

---

## 12. Quick Reference: Before / After

| Concern | Today | After |
| --- | --- | --- |
| Handle declaration | Generator's `BuildHandleDefinitions` static method | Object-initializer list on the parameter |
| Parameter reference | `parameters["connect.curveStartControlPoint"]` (string) | Direct field via `nameof` (rename-aware; resolved at build time) |
| Parent / child | Integer `Key` / `ParentKey` constants | `Parent = nameof(OtherParameter)` |
| Mode filtering for handles | Implicit (only the active mode's generator runs) | Explicit `Modes` bitflag (same as parameters) |
| Computed handles | Override `OnParameterHandleDragged` + object-identity dispatch | `ComputeFromPosition` / `ComputePosition` delegates on the spec |
| Generator's role | Geometry + handle authoring | Geometry only |
| Drag dispatch | Per-type virtuals (`OnPositionHandleDragged`, `OnCircleHandleDragged`, etc.) | Single generic dispatch reading the spec's delegates |
| Co-location of metadata | Parameter on the tool, handle in the generator | Both on the tool, handle nested under its parameter |
| Relationship cardinality | One handle = one parameter (1:1, fixed) | One parameter = zero or more handles (1:0..N) |
| `m_UpdateNeeded` wiring (§8.1) | Manual `OnChanged += () => ...` per parameter in each tool's `OnCreate` | Auto-subscribed during parameter discovery |
| Parent → child propagation (§8.2) | Imperative `PropagateToChildren` + O(n²) `SyncChildParameterPositions` per drag tick | Subscription wired once at handle creation; fires only on actual change |
| `HandleConstraints` authoring | Inline `NT_HandleConstraints` struct init | Static helpers (`HandleConstraints.AxisXZ`, `Plane(...)`) |
