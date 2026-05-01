# Parameter System Refactor

Design document for replacing the per-tool `Config` struct pattern with a declarative parameter system. Written 2026-05-01.

---

## 1. Context

### Current architecture

Each tool (Parallel, Connect, Generate, RoadShape) owns a `Config` struct that holds its parameters. The struct flows through several layers:

```
React UI  <-->  UISystem binding  <-->  Tool System  <-->  Burst Job
                                              ^
                                              |
                                          Handles (drag updates config field)
```

Relevant files:
- `NetworkTools.Mod/Systems/Tools/Parallel/Core/ParallelConfig.cs`
- `NetworkTools.Mod/Systems/Tools/Connect/Core/ConnectConfig.cs`
- `NetworkTools.Mod/Systems/Tools/Generate/Core/GenerateConfig.cs`
- `NetworkTools.Mod/Systems/Tools/RoadShape/Core/ShapeTransformConfig.cs`
- `NetworkTools.Mod/Systems/UI/UISystem.cs` (binding setup, handler dispatch)
- `NetworkTools.Mod/UI/src/gameBindings.ts` (manually mirrored TS types)

Generator/transform interfaces already exist and stay in scope:
- `IConnectionGenerator`, `IHandleableConnectionGenerator` (Connect)
- `IGenerator`, `IHandleableGenerator` (Generate)
- `IPathTransformation`, `IHandleableTransformation` (RoadShape)

### Pain points

1. **Serialization boilerplate.** Each `Config` manually implements `IJsonWritable.Write` / `IJsonReadable.Read` per field. Two configs (Connect, Generate) are still placeholders.
2. **TS / C# type duplication.** Every config shape, enum, and default is restated in `gameBindings.ts`. Drift is silent.
3. **Metadata scattering.** Min/max/default values exist as C# constants on the config, again as binding defaults in `UISystem.OnCreate`, again as TS defaults, again as React slider props.
4. **Handle key dispatch switches.** Handles use opaque `int` keys (`HandleKeys.EaseInLength`, etc.). Drag callbacks switch on key to find the field to update.
5. **Manual revision tracking.** RoadShape uses a `ShapeConfigRevision` counter to push handle-driven config edits back to UI. Other tools handle this ad hoc or not at all.

### What is *not* a problem (do not change)

- **Configs as structs for Burst.** Burst jobs cannot take classes. The struct form is load-bearing for the execution path.
- **Mode dispatch in jobs.** Burst forbids virtual dispatch. The `switch (Mode)` in jobs is mandatory and stays.
- **Generator interfaces.** Mode-specific logic lives in the right place.
- **Mode-specific handle construction.** Loop mode's `CircleHandle` and SimpleCurve's `PositionHandle`s are genuinely different shapes. Generators legitimately need a switch to construct the right ones.

---

## 2. Goals

- Declare each tool's parameters once, in C#, with all metadata co-located.
- Auto-generate TS types, keys, and enum mirrors at build time.
- Auto-bind parameters to UI via reflection (one binding per parameter).
- Auto-serialize via the parameter base class (no per-config Write/Read).
- Bind handles to parameters by reference (eliminate `HandleKeys` and dispatch switches).
- Keep the Burst execution path unchanged: snapshot parameters into a struct at job-schedule time.
- Updates are granular: a single edit (slider drag, handle drag, mode change) pushes only the changed parameter, not a whole-config struct. No monolithic config serialization on every edit.
- Support reset semantics: reset a single parameter to its declared default, and reset all parameters in a tool to defaults.

### Non-goals

- Changing how generators dispatch on mode in jobs.
- Eliminating mode-specific handle construction.
- Replacing `ExtendedUISystemBase` or the Colossal binding system.
- Runtime schema delivery to React (deferred; build-time codegen is sufficient for v1).

---

## 3. Architecture Overview

```
Tool (class, declared in C#)
|
+-- Parameters (fields)              <-- declarative, reflection-driven
|   |
|   +-- key, type, default, range, modes (metadata)
|   +-- current value + change event
|   +-- registered as one Colossal binding each
|   +-- serialized via base class
|
+-- Generators (per mode, unchanged interface)
|   |
|   +-- construct handles holding parameter refs
|   +-- read parameters via job-config snapshot
|
+-- Job snapshot (thin glue)         <-- params -> struct for Burst
    |
    +-- one BuildJobConfig() per tool, mechanical
    +-- candidate for source generation later
```

What goes away:
- `HandleKeys` enums and their switches.
- Manual `IJsonWritable.Write` / `Read` per config.
- TS type/enum/default duplication in `gameBindings.ts`.
- Per-config min/max/default constants scattered across C# and TS.

What stays (intentionally):
- Burst job structs (now derived as snapshots, not the canonical store).
- Mode dispatch in jobs.
- Generator-owned handle construction.

---

## 4. Parameter Base Classes

### Hierarchy

```csharp
public abstract class ParameterBase {
    public string Key   { get; }
    public int    Modes { get; }  // bitflag: which modes use this param (0 = all)

    public event Action OnChanged;

    // Reset to declared default. Fires OnChanged.
    public abstract void ResetToDefault();
    // Fire OnChanged unconditionally (e.g., to re-sync UI on tool activation).
    public void ForceNotify() => OnChanged?.Invoke();
}

public abstract class Parameter<T> : ParameterBase {
    public T Default { get; }
    public T Value {
        get => m_Value;
        set {
            if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
            m_Value = value;
            RaiseChanged();
        }
    }
}

public class FloatParameter : Parameter<float> { public float Min, Max; ... }
public class IntParameter   : Parameter<int>   { public int   Min, Max; ... }
public class BoolParameter  : Parameter<bool>  { ... }
public class EnumParameter<TEnum> : Parameter<TEnum>, IEnumParameter where TEnum : struct, Enum { ... }
public class Float3Parameter : Parameter<float3> { ... }
// Add Quaternion, Color, etc. only when needed.

// IEnumParameter — non-generic handle for UISystem binding registration
public interface IEnumParameter {
    string Key      { get; }
    int    IntValue { get; set; }
}
```

### Parameter management lives on `NT_BaseToolSystem`

There is **no separate tool-definition class**. Parameters are declared as `public` fields directly on the concrete tool system. `NT_BaseToolSystem` provides discovery, reset, and the parameter list via a new partial file `BaseToolSystem.Parameters.cs`:

```csharp
// BaseToolSystem.Parameters.cs
public abstract partial class NT_BaseToolSystem {
    private ParameterBase[] m_ToolParameters;

    // All ParameterBase fields declared on the concrete tool, in declaration order.
    // Lazily discovered via reflection and cached per instance.
    public IReadOnlyList<ParameterBase> Parameters =>
        m_ToolParameters ??= ParameterSchema.Discover(this);

    public void ResetAll() { foreach (var p in Parameters) p.ResetToDefault(); }

    public bool Reset(string key) {
        foreach (var p in Parameters) {
            if (p.Key == key) { p.ResetToDefault(); return true; }
        }
        return false;
    }
}
```

`ParameterSchema.Discover(object instance)` reflects the concrete type's **public instance fields**, filters for `ParameterBase` subtypes, and caches `FieldInfo[]` per type. Tool systems may have many other fields (ECS queries, native lists, etc.) — they are all private or non-`ParameterBase`, so reflection is unambiguous.

### Parameter declaration example (Parallel)

```csharp
public partial class NT_ParallelToolSystem : NT_PathSelectionToolSystem, ... {
    public FloatParameter              HorizontalOffset    = new("parallel.horizontalOffset", 20f, 0f, 80f);
    public FloatParameter              VerticalOffset      = new("parallel.verticalOffset",   0f,  0f, 80f);
    public EnumParameter<ParallelSide> HorizontalDirection = new("parallel.horizontalDirection", ParallelSide.Right);
    public EnumParameter<VerticalSide> VerticalDirection   = new("parallel.verticalDirection",   VerticalSide.Up);
    public BoolParameter               ReverseDirection    = new("parallel.reverseDirection", false);
}
```

### Parameter declaration example (Connect, with mode tags)

```csharp
public partial class NT_ConnectToolSystem : NT_BaseToolSystem, ... {
    public EnumParameter<ConnectMode> Mode = new(
        key: "connect.mode",
        @default: ConnectMode.None);

    // Shared across all modes
    public Float3Parameter StartPosition  = new("connect.startPosition",  default);
    public Float3Parameter EndPosition    = new("connect.endPosition",    default);
    public Float3Parameter StartDirection = new("connect.startDirection", default);
    public Float3Parameter EndDirection   = new("connect.endDirection",   default);

    // SimpleCurve-only
    public Float3Parameter CurveStartControlPoint = new(
        key: "connect.curve.startControl",
        modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve);
    public Float3Parameter CurveEndControlPoint = new(
        key: "connect.curve.endControl",
        modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve);

    // Loop-only
    public FloatParameter LoopRadius = new(
        key: "connect.loop.radius",
        @default: 50f, min: 1f, max: 500f,
        modes: (int)ConnectMode.Loop);
}
```

`Modes` is metadata: it informs UI grouping, codegen output, and selective serialization. It does **not** gate runtime reads; generators read whatever they need.

---

## 5. UI Binding Setup (Reflection)

`NT_UISystem.OnCreate` discovers parameters once at startup and registers a binding per parameter.

### Pseudo code

```csharp
// In NT_UISystem.OnCreate — register per tool system that has parameters:
RegisterToolParameterBindings(m_NtParallelToolSystem);

void RegisterToolParameterBindings(NT_BaseToolSystem tool) {
    foreach (var param in tool.Parameters)
        RegisterParameterBinding(param);
}

void RegisterParameterBinding(ParameterBase param) {
    if (param is FloatParameter fp) {
        var b = CreateBinding(fp.Key, fp.Value, (float v) => fp.Value = v);
        fp.OnChanged += () => b.Value = fp.Value;
    } else if (param is BoolParameter bp) {
        var b = CreateBinding(bp.Key, bp.Value, (bool v) => bp.Value = v);
        bp.OnChanged += () => b.Value = bp.Value;
    } else if (param is IEnumParameter ep) {
        // Enums transported as int over the binding bridge
        var b = CreateBinding(ep.Key, ep.IntValue, (int v) => ep.IntValue = v);
        ep.OnChanged += () => b.Value = ep.IntValue;
    }
}
```

`ParameterSchema.Discover` caches `FieldInfo[]` per concrete type. First access on each tool instance runs reflection; all subsequent lookups are O(n) array iteration only. No reflection on hot paths.

On tool activation, force-notify all parameters to re-sync UI bindings:

```csharp
foreach (var p in m_NtParallelToolSystem.Parameters)
    p.ForceNotify();
```

### Reset triggers

Expose two triggers for the UI:

```csharp
CreateTrigger<string>("RESET_PARAM", key => (m_ToolSystem.activeTool as NT_BaseToolSystem)?.Reset(key));
CreateTrigger("RESET_TOOL",          ()  => (m_ToolSystem.activeTool as NT_BaseToolSystem)?.ResetAll());
```

Resetting a parameter fires its `OnChanged`, which pushes the new value through its binding the same way any other edit would.

### Per-parameter bindings vs single config binding

Today: one binding per tool, full struct re-pushed on any field change.
After: one binding per parameter, only the changed value pushed.

Net effect: more bindings (~30 across all tools, vs ~5), smaller per-edit payloads, granular React subscriptions.

---

## 6. Handles Bound to Parameters

### Goal

Replace `HandleKeys` enum + `OnParameterHandleDragged` switch with direct parameter references on the handle.

### Today

```csharp
// Construction
new TransformHandleDefinition { Key = HandleKeys.EaseInLength, ... }

// Drag callback (RoadShapeToolSystem.Handles.cs)
switch (key) {
    case HandleKeys.EaseInLength:
        ShapeTransformConfig.EaseInLength = ...;
        ShapeConfigRevision++;
        break;
    ...
}
```

### After

```csharp
// Construction (in generator's GetHandleDefinitions)
new TransformHandleDefinition {
    Parameter = tool.EaseInLength,   // direct reference
    Kind      = HandleKind.Parameter,
    ...
}

// Drag callback (in tool system, generic for all tools)
protected override void OnParameterHandleDragged(Entity handle, ParameterBase parameter, float value) {
    ((FloatParameter)parameter).Value = value;
    // Parameter.OnChanged fires -> binding pushes to UI -> m_UpdateNeeded set
}
```

### Layer separation

- **Handle -> parameter binding:** direct, declarative, no key dispatch. Applies to all tools.
- **Which handles exist per mode:** stays in generators. The `switch (Mode)` in `GetHandleDefinitions` is real work (different handle shapes per mode), not a dispatch artifact. Keep it.

For tools with no modes (Parallel), handles can be declared as fields on the tool alongside parameters. For tools with mode-specific handle shapes (Connect, Generate, RoadShape), generators continue to construct them.

### Multi-parameter handles

Some handles update more than one parameter (e.g., a position+direction handle). Support via:

```csharp
new TransformHandleDefinition {
    Kind = HandleKind.PositionAndDirection,
    PositionParameter  = tool.StartPosition,
    DirectionParameter = tool.StartDirection,
}
```

The drag callback writes to whichever parameters are non-null.

---

## 7. Job Snapshot (Burst Glue)

Burst cannot consume class-based parameter objects. Each tool keeps a Burst-friendly struct, but it is now a *derived* snapshot, not the canonical store.

### Pattern

```csharp
public struct ParallelJobConfig {
    public float HorizontalOffset;
    public float VerticalOffset;
    public ParallelSide HorizontalDirection;
    public VerticalSide VerticalDirection;
    public bool ReverseDirection;
}

// In ParallelToolSystem, just before scheduling the job:
var jobConfig = new ParallelJobConfig {
    HorizontalOffset    = m_Tool.HorizontalOffset.Value,
    VerticalOffset      = m_Tool.VerticalOffset.Value,
    HorizontalDirection = m_Tool.HorizontalDirection.Value,
    VerticalDirection   = m_Tool.VerticalDirection.Value,
    ReverseDirection    = m_Tool.ReverseDirection.Value,
};
```

### Notes

- This is mechanical. A future source generator could emit `BuildJobConfig()` from the tool definition. v1 writes them by hand; the surface area is small.
- Generators' `GenerateConnection` / `Process` methods take the snapshot struct, exactly as today.
- The snapshot is computed each time the job is scheduled. Cheap; no allocations if structs are stack-local.

---

## 8. Build-Time TypeScript Codegen

### What gets generated

A single `.generated.ts` file emitted into `UI/src/` by the C# build (via MSBuild task that runs a small C# console tool).

Contents:

```typescript
// AUTO-GENERATED. Do not edit.

export enum ParallelSide { Left = 0, Right = 1 }
export enum VerticalSide { Up = 0, Down = 1 }
export enum ConnectMode  { None = 0, SimpleCurve = 1, ComplexCurve = 2, Loop = 3 }
// ...

export const PARAM_KEYS = {
    parallel: {
        horizontalOffset:    "parallel.horizontalOffset",
        verticalOffset:      "parallel.verticalOffset",
        horizontalDirection: "parallel.horizontalDirection",
        // ...
    },
    connect: { ... },
    generate: { ... },
    roadShape: { ... },
} as const;

export const PARAM_META = {
    "parallel.horizontalOffset": {
        type: "float", default: 20, min: 0, max: 80, modes: 0
    },
    // ...
} as const;
```

### Source

The generator reflects on (or parses) all `NT_ToolDefinition` subclasses, walks their parameter fields, emits matching TS.

### Consumption in React

```typescript
import { PARAM_KEYS } from "./generated/parameters";
import { useBinding } from "./bindings";

const offset = useBinding<number>(PARAM_KEYS.parallel.horizontalOffset);
```

### Implementation choice

**MSBuild PreBuild task** — a small console exe that loads the compiled assembly, reflects, writes the `.ts` file. Easier to write, requires assembly to compile first (chicken/egg avoided by emitting only on the second pass or by parsing source instead of loading the assembly).

---

## 9. Migration Plan

### Phase 1 — Foundation + Parallel migration (smallest viable first PR) ✅

Ship the parameter system end-to-end against the simplest tool. Parallel has no modes, no float3, no complex handles — ideal validation target.

- Implement `ParameterBase`, `Parameter<T>`, and concrete subclasses (`Float`, `Int`, `Bool`, `Enum<T>`, `Float3`).
- Implement `ParameterSchema.Discover(object)` with cached per-type `FieldInfo[]` reflection.
- Add `Parameters`, `Reset(key)`, `ResetAll()` to `NT_BaseToolSystem` via `BaseToolSystem.Parameters.cs`.
- Implement reflection-driven binding registration in `NT_UISystem` (`RegisterToolParameterBindings`, `RegisterParameterBinding`).
- Declare parameters inline on `NT_ParallelToolSystem` (no separate definition class).
- Wire `OnChanged → m_UpdateNeeded = true` in `NT_ParallelToolSystem.OnCreate`.
- Replace `UpdateConfig(ParallelConfig)` and `CurrentConfig` with snapshot built from inline parameters.
- Add `ParallelJobConfig` snapshot struct; update job to use it.
- Add `RESET_PARAM` and `RESET_TOOL` triggers.
- Update React Parallel panel to consume five individual parameter bindings.

End-to-end: UI edit → binding → parameter → `OnChanged` → `m_UpdateNeeded` → snapshot → job → network. Burst job code unchanged.

### Phase 2 — Build-time TS codegen ✅

- Add MSBuild task project (`NetworkTools.Codegen`, net8.0 console app) that emits `parameters.generated.ts` (enums, `PARAM_KEYS`, `PARAM_META`, `PARAM_BINDINGS`)
- Wire into UI build via `GenerateParametersTS` MSBuild target (runs before `BuildUI`).
- Convert the Parallel React panel to consume `PARAM_BINDINGS` and `PARAM_META` instead of hardcoded keys/ranges/bindings.
- Remove duplicated `ParallelSide`/`VerticalSide` enums and parallel `TwoWayBinding` entries from `gameBindings.ts` (now generated).

### Phase 3 — Migrate remaining tools

Order: Generate -> Connect -> RoadShape (increasing complexity).

For each:
- Declare parameters inline on the tool system class (no separate definition class).
- Wire `OnChanged → m_UpdateNeeded = true` in `OnCreate`.
- Update generators to read from the `*JobConfig` snapshot struct (mostly mechanical rename).
- Replace `HandleKeys.X` references with parameter refs.
- Construct mode-specific handles inside generators (unchanged), but with parameter refs instead of keys.
- Drop the now-unused `*Config.cs` canonical struct.

See section 10 for tool-specific gotchas.

### Phase 4 — Cleanup

- Delete `HandleKeys` enum.
- Delete TS type duplication from `gameBindings.ts` (replaced by generated file).
- Delete revision counters (`ShapeConfigRevision` etc.) — replaced by per-parameter `OnChanged` events.
- Update this doc with the final shape.

---

## 10. Open Questions / Risks / Migration Gotchas

### General

1. **`float3` over the binding bridge.** If `ValueWriter<float3>` doesn't work, decompose into three float bindings under one parameter.
2. **Two-way sync edge cases.** Handle drag updates parameter -> fires `OnChanged` -> pushes to UI binding. UI slider updates binding -> writes to parameter -> fires `OnChanged` -> needs to NOT bounce back. The value-equality short-circuit in `Parameter<T>.Value` setter handles this; verify with a Parallel slider during Phase 1.
3. **Codegen failure modes.** If the MSBuild task fails, the TS build should fail loudly. Don't let stale `.generated.ts` ship.
4. **Mode-tag bitflag width.** `int` modes field assumes <32 modes per tool. True for current and foreseeable scope.

### Tool-specific gotchas

8. **`ShapeTransformConfig` factory methods.** `Preserve()`, `SlopeLinear()`, `SlopeEaseInOut()`, `SlopeArch()`, `CurveSmooth()` etc. are called from `UISystem.Handlers.HandleUpdateShapeConfig`'s template-change switch. They reset the config to template-specific defaults. When migrating RoadShape, the equivalent must exist as either:
   - Per-template "preset" methods on `NT_RoadShapeToolSystem` (e.g., `ApplySlopeLinearPreset()`), each calling `ResetAll()` then setting template-specific parameters, **or**
   - Mode-conditional defaults baked into each parameter via the `modes` tag plus a `ResetForMode(mode)` helper.
   The first is closer to current behavior; pick during Phase 3 RoadShape migration.

9. **`ConnectConfig` / `GenerateConfig` contextual init.** These configs have `float3` fields (positions, directions) that get populated from selected nodes at `SetMode()` time, not from declared defaults. Mirror this with a `ResetWith(args)` method on the tool that resets parameters AND seeds runtime context (start/end positions). Don't try to encode contextual defaults as attribute metadata.

### Migration-window concerns (during Phase 3)

10. **`m_UpdateNeeded` flag.** Until a tool fully migrates, leave its update flag flow alone. Parameter `OnChanged` should mark `m_UpdateNeeded = true` for the owning tool system — same downstream effect as today's `UpdateConfig` calls.

11. **`ShapeConfigRevision` counter.** Stays in place until RoadShape migrates. Replaced by per-parameter `OnChanged` only in Phase 4 cleanup.

12. **JSON property name stability mid-migration.** During Phase 3, partially migrated tools still flow over their existing config bindings while others use per-parameter bindings. Don't rename JSON keys mid-migration; rename only at end-of-phase cleanup if needed.

---

## 11. Reference: Files Likely To Change

| Area | Files |
| --- | --- |
| New | `Systems/Parameters/ParameterBase.cs`, `Parameter.cs`, `FloatParameter.cs`, `IntParameter.cs`, `BoolParameter.cs`, `EnumParameter.cs` |
| New | `Systems/Parameters/ParameterSchema.cs` |
| New | `Systems/Tools/Base/BaseToolSystem.Parameters.cs` (partial — adds `Parameters`, `Reset`, `ResetAll`) |
| New | Per-tool `*JobConfig.cs` snapshot structs |
| New | `NetworkTools.Codegen/` — net8.0 console app for TS codegen (Phase 2 ✅) |
| New | `UI/src/generated/parameters.generated.ts` (Phase 2 ✅) |
| Modified | `Systems/UI/UISystem.cs` (reflection-driven binding setup) |
| Modified | `Systems/UI/UISystem.Handlers.cs` (generic handle dispatch) |
| Modified | `Systems/Tools/Parallel/*` (Phase 1 ✅) |
| Modified | `Systems/Tools/Generate/*`, `Connect/*`, `RoadShape/*` (Phase 3) |
| Deleted | `HandleKeys.cs` (Phase 4) |
| Deleted | Per-tool `*Config.cs` canonical structs (Phase 4) |
| Modified | `UI/src/gameBindings.ts` (drop duplicated types, Phase 4) |

---

## 12. Quick Reference: Before / After

| Concern | Today | After |
| --- | --- | --- |
| Parameter declaration | C# const + binding default + TS default + React slider props | `public FloatParameter X = new(...)` on tool system, codegen handles the rest |
| Parameter management | Each tool class owns it ad hoc | `NT_BaseToolSystem` provides `Parameters`, `Reset`, `ResetAll` |
| Separate definition class | n/a | **None** — params are inline on the tool system |
| Serialization | Manual `Write` / `Read` per config | `Parameter<T>` base class |
| TS types | Hand-mirrored in `gameBindings.ts` | Build-time generated (Phase 2) |
| UI binding | One per config struct | One per parameter |
| Handle -> field | `HandleKeys` enum + dispatch switch | Direct parameter reference |
| Mode dispatch in jobs | `switch (Mode)` -> generator | Unchanged (Burst-mandated) |
| Mode-specific handle shapes | Generator's `GetHandleDefinitions` switch | Unchanged |
| Burst job input | `Config` struct (canonical) | `JobConfig` struct (snapshot from parameters) |
| Change notification | Ad hoc + revision counters | `Parameter.OnChanged` events |
