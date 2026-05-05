# Handle System Refactor (v2)

Nest handle declarations under their owning parameter, eliminate per-tool `BuildHandleDefinitions` methods, the string-keyed parameter lookup, and the residual per-type drag dispatch. Written 2026-05-04.

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
- **Parameter system.** Additive only — `Handles` becomes a new property on `Parameter<T>`.

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

**Drag:** raycast hits an entity → read `m_HandleEntries[entity]` → `ComputeFromPosition` if present, otherwise direct write → `SetWithoutNotify` to the parameter → `NotifyBindingForParameter` + `m_UpdateNeeded = true`.

**Reverse-sync:** when a parameter changes from a non-handle source (slider, reset, external mutation), `OnChanged` fires → spec's `ComputePosition` (or direct value mirror) → `SetHandlePosition`.

Spec compute delegates and resolvers run on the main thread (`NT_BaseToolSystem` is managed). Generator geometry stays in Burst-compiled jobs.

---

## 3. Handle Spec Types

```csharp
public interface IHandleSpec {
    HandleTypeFlags         TypeFlags    { get; }
    string                  Parent       { get; }   // nameof(otherParameter); null = root
    NT_HandleConstraints?   Constraints  { get; }
    float                   Radius       { get; }
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
| `RotationHandle` | `Float3Parameter` (direction) | Center anchored via `Parent`; plane defined by `Normal` + `ReferenceDirection`. |
| `CircleHandle` | `FloatParameter` (radius) | Center anchored via `Parent`. |
| `ComputedPositionHandle` | `FloatParameter` | Requires both compute delegates; renders as position, stores scalar. |

The `IHandleSpec<T>` generic parameter enforces that `Float3Parameter` only accepts `IHandleSpec<float3>[]` and `FloatParameter` only `IHandleSpec<float>[]` — type mismatches are compile errors. `nameof(...)` references are rename-aware (IDE rename refactors carry through) but resolve at tool activation, not compile.

### Resolver fields

`Parent` is the handle's anchor parameter — the float3 that determines where the handle sits in world space. It serves all anchor roles: root position, circle/rotation center, parent-drag propagation source. Two extra resolvers cover non-positional anchoring:

| Field | Used by | Default |
| --- | --- | --- |
| `Parent` | anchor point (root position, circle/rotation center, parent-drag source) | null (anchor = parameter's own value) |
| `ReferenceDirectionFrom` | `RotationHandle` zero-angle direction | inline `ReferenceDirection` literal |
| `NormalFrom` | `CircleHandle` / `RotationHandle` plane normal | inline `Normal` literal or Y-up |

Resolvers fire at build time and re-fire on the referenced parameter's `OnChanged`. For one-off compositions that don't fit, use `ComputePosition` / `ComputeFromPosition` delegates.

---

## 4. Parameter Integration

```csharp
public abstract class Parameter<T> : ParameterBase {
    public IHandleSpec<T>[] Handles { get; init; }    // null = no spatial handle

    public void SetWithoutNotify(T value) {
        if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
        m_Value = value;
    }
    // ...existing fields...
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

private Dictionary<Entity, HandleEntry> m_HandleEntries;

protected void RebuildHandlesForActiveMode() {
    CancelHandleInteraction();
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

Mode change triggers rebuild via the mode parameter's `OnChanged` event. `CancelHandleInteraction` defends against rebuild firing mid-drag.

### Drag dispatch

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

`NotifyBindingForParameter` pokes the Colossal binding bridge for `param` directly. Drag dispatch never fires `OnChanged`, so reverse-sync subscribers don't see handle-driven changes — bounce loop avoided.

### Reverse-sync

Wired once per parameter-with-handles in `RebuildHandlesForActiveMode`:

```csharp
param.OnChanged += () => {
    foreach (var entity in HandlesFor(param)) {
        var spec = m_HandleEntries[entity].Spec;
        float3 pos;
        switch (param) {
            case Float3Parameter f3p:
                pos = ((IHandleSpec<float3>)spec).ComputePosition?.Invoke(this, f3p.Value) ?? f3p.Value;
                break;
            case FloatParameter fp:
                pos = ((IHandleSpec<float>)spec).ComputePosition?.Invoke(this, fp.Value) ?? DefaultPositionFor(param, spec);
                break;
            default: continue;
        }
        SetHandlePosition(entity, pos);
    }
};
```

Only `Value =` writes (UI sliders, `ResetToDefault`, external mutation) reach this subscriber.

### Bulk seeding

Tools seed parameters in bulk inside `InitializeConfig`. Two rules:

1. Bulk seeding precedes `RebuildHandlesForActiveMode` (otherwise §6.2's parent-child closures capture stale `lastParentPos`).
2. Bulk seeding uses `SetWithoutNotify(...)`. The post-seed rebuild creates handles at the freshly-seeded values. Bindings refresh in one batch via `param.ForceNotify()` after seeding completes.

`ResetAll` / `Reset(key)` keep the regular setter — they're user-facing intents that should fire `OnChanged`.

---

## 6. Adjacent Improvements

### 6.1 Auto-wire `m_UpdateNeeded` during parameter discovery

Replaces the manual `OnChanged += () => m_UpdateNeeded = true;` line every tool's `OnCreate` writes for every parameter:

```csharp
// BaseToolSystem.Parameters.cs — extend the discovery step
foreach (var p in Parameters) {
    p.OnChanged += MarkUpdateNeeded;
}

protected void MarkUpdateNeeded() => m_UpdateNeeded = true;
```

Tools that need additional per-parameter side-effects still subscribe manually.

### 6.2 Parent → child propagation via subscription

Replaces today's imperative `PropagateToChildren` + O(n²) `SyncChildParameterPositions` per drag tick ([BaseToolSystem.Handles.cs:430-453, 808-825](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:430)) with a subscription wired at handle creation:

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

        childParam.SetWithoutNotify(childParam.Value + delta);
        NotifyBindingForParameter(childParam);
        // Child's reverse-sync subscription moves the child handle entity.
    };
}
```

Per-drag-tick cost drops from O(parents × all_handles) to O(direct_children).

---

## 7. Migration Plan

Two phases. No dual-dispatch shim — Phase 1 flips every tool at once and deletes the old machinery in the same PR.

### Phase 0 — Scaffolding

Mechanical, no behavior change.

- **§6.1** Auto-wire `MarkUpdateNeeded` in `BaseToolSystem.Parameters.cs`. Remove the manual lines from every tool's `OnCreate`.
- Add `IHandleSpec`, `IHandleSpec<T>`, the four concrete spec types, and the `Handles` property on `Parameter<T>`.
- Add `Parameter<T>.SetWithoutNotify` and a `NotifyBindingForParameter(ParameterBase)` helper on `BaseToolSystem`.
- Implement scoped-subscription cleanup (see §8). Either an `IDisposable` token list cleared on `DisposeHandles`, or a `Parameter.OnChangedScoped` overload. **Load-bearing for Phase 1.**

### Phase 1 — One-shot migration

**New dispatch infrastructure** (`BaseToolSystem.Handles.cs`):
- `RebuildHandlesForActiveMode` walking `Parameters[].Handles` inline.
- `ResolveParentLinks` (`nameof` → `Entity`).
- `m_HandleEntries` map (parameter + spec).
- Generic `DispatchDrag` using `SetWithoutNotify` + `NotifyBindingForParameter`.
- Reverse-sync subscriptions via Phase 0 scoped-cleanup.
- §6.2 parent-child subscriptions.
- `HandleConstraints` static helpers (`AxisXZ`, `Plane(normal)`, `LockY`).

**Tool migrations** (all in this PR):
- **Connect.** Inline handles for SimpleCurve, Loop, ComplexCurve. Loop's rotation handles validate `Parent` + `ReferenceDirectionFrom`.
- **RoadShape.** `ComputedPositionHandle`s for `EaseInLength` / `EaseOutLength` / etc. with projection delegates.
- **Generate.** Inline handles for Grid mode. Circle has none.
- **Parallel.** Slider-only; no handles.

**Bulk-seeding conversion.** Every `InitializeConfig` switches to `SetWithoutNotify(...)` followed by rebuild.

**Deletions** (same PR):
- `TransformHandleDefinition` struct.
- Integer `Key` / `ParentKey` machinery and the two-pass `CreateHandlesFromDefinitions`.
- Per-type drag virtuals: `OnPositionHandleDragged`, `OnCircleHandleDragged`, `OnRotationHandleDragged`, `OnParameterHandleDragged`.
- `BuildHandleDefinitions` on every generator and transform.
- `Connect.Handles.cs`, `Generate.Handles.cs`, `RoadShapeToolSystem.Handles.cs`.
- `IHandleableConnectionGenerator`, `IHandleableGenerator`, `IHandleableTransformation` interfaces.
- `PropagateToChildren`, `SyncChildParameterPositions`.

**Verification:**
- Connect: SimpleCurve → Loop → ComplexCurve mode switching, drag, slider sync, reset, parent-drag-moves-children.
- RoadShape: ease-length drag (computed path); template presets (`SlopeLinear()`, `CurveSmooth()`, etc.) firing `OnChanged` so reverse-sync repositions handles.
- Generate: Grid drag.
- Bounce: validate Connect's straightforward handles, then RoadShape's projection math.
- Subscription audit: cycle modes 50× across all tools; `Parameter.OnChanged` subscriber count returns to baseline after each rebuild.
- Overlay rendering unchanged (entities still carry `NT_Handle*` components, including the `BezierControlPoint` flag now driven by `PositionHandle.Style`).

---

## 8. Risks

1. **Subscription cleanup on rebuild — load-bearing.** §6.2 parent-child subscriptions and §5 reverse-sync subscriptions both attach to `Parameter.OnChanged`. Mode changes rebuild the handle set; old subscriptions must detach or they accumulate per mode-cycle and pin tool state past tool deactivation. Closures capture parameter refs, last-position state, and the spec — leaks are real GC pressure. Implement scoped cleanup in Phase 0.

2. **Spec immutability.** Specs are shared across rebuilds. If anything mutates spec fields at runtime (e.g., `Constraints` changing per-mode), move that mutation to a per-`Entity` map, not the shared spec.

3. **Handle entity identity on rebuild.** Mode change destroys and recreates handle entities. Anything outside the base system holding a handle `Entity` must invalidate on rebuild. Grep during Phase 1.

4. **Parent group semantics for multi-handle parameters.** When `Parent = nameof(X)` and `X` has multiple handles, "follow which?" needs an answer. Today every parent-side parameter has exactly one handle, so the choice is invisible. Keep `Parent` as `string` for now; the resolver shape leaves room for a `ParentSelector` extension when a real multi-handle parameter ships.

5. **TS-binding codegen.** Re-run codegen after Phase 0 and diff to confirm the new `Handles` property doesn't surface in bindings (it shouldn't — handles aren't bindable, codegen targets binding key + value type).

---

## 9. File Reference

| Phase | Action | Files |
| --- | --- | --- |
| 0 | New | `Systems/Handles/IHandleSpec.cs`, `PositionHandle.cs`, `CircleHandle.cs`, `RotationHandle.cs`, `ComputedPositionHandle.cs` |
| 0 | Modify | `Systems/Tools/Parameters/Parameter.cs` (`Handles`, `SetWithoutNotify`) |
| 0 | Modify | `Systems/Tools/Base/BaseToolSystem.Parameters.cs` (§6.1, `NotifyBindingForParameter`) |
| 1 | New | `Systems/Handles/HandleConstraints.cs` (static helpers) |
| 1 | Modify | `Systems/Tools/Base/BaseToolSystem.Handles.cs` (full dispatch rewrite) |
| 1 | Modify | `Systems/Tools/{Connect,RoadShape,Generate}/NT_*ToolSystem.cs` (handle declarations) |
| 1 | Modify | `Systems/Tools/{Connect,RoadShape,Generate}/{Generators,Transforms}/*.cs` (drop `BuildHandleDefinitions`) |
| 1 | Delete | `Systems/Tools/{Connect,RoadShape,Generate}/*.Handles.cs` |
| 1 | Delete | `Systems/Tools/Base/TransformHandleDefinition.cs` |
| 1 | Delete | `IHandleableConnectionGenerator`, `IHandleableTransformation`, `IHandleableGenerator` |
