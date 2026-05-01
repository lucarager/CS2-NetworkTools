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
    public string Key { get; }
    public string LabelKey { get; }   // localization id, optional
    public int    Modes { get; }      // bitflag: which modes use this param (0 = all)

    public event Action OnChanged;

    public abstract Type ValueType { get; }
    public abstract object GetValueBoxed();
    public abstract void   SetValueBoxed(object value);

    public abstract void WriteJson(IJsonWriter w);
    public abstract void ReadJson(IJsonReader r);

    // Reset to declared default. Fires OnChanged.
    public abstract void ResetToDefault();

    // For codegen: emit a TS type descriptor
    public abstract ParameterDescriptor Describe();
}

public class Parameter<T> : ParameterBase {
    private T m_Value;
    public T Value {
        get => m_Value;
        set {
            if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
            m_Value = value;
            OnChanged?.Invoke();
        }
    }
    public T Default { get; }
    // ...
}

public class FloatParameter : Parameter<float> {
    public float Min { get; }
    public float Max { get; }
    public FloatParameter(string key, float @default, float min, float max, int modes = 0) { ... }
}

// At the tool level
public abstract class NT_ToolDefinition {
    // Reflect parameter fields once per tool type, cache statically.
    public IReadOnlyList<ParameterBase> Parameters => ParameterSchema.For(GetType());

    // Reset every parameter on this tool to its declared default.
    public void ResetAll() {
        foreach (var p in Parameters) p.ResetToDefault();
    }

    // Reset by key (single-parameter reset surfaced to UI).
    public bool Reset(string key) {
        foreach (var p in Parameters) {
            if (p.Key == key) { p.ResetToDefault(); return true; }
        }
        return false;
    }
}

public class IntParameter   : Parameter<int>  { public int Min, Max; ... }
public class BoolParameter  : Parameter<bool> { ... }
public class EnumParameter<TEnum> : Parameter<TEnum> where TEnum : struct, Enum { ... }
public class Float3Parameter : Parameter<float3> { ... }
// Add Quaternion, Color, etc. only when needed.
```

### Tool definition example (Parallel)

```csharp
public class ParallelTool : NT_ToolDefinition {
    public FloatParameter HorizontalOffset = new(
        key: "parallel.horizontalOffset",
        @default: 20f, min: 0f, max: 80f);

    public FloatParameter VerticalOffset = new(
        key: "parallel.verticalOffset",
        @default: 0f, min: -50f, max: 50f);

    public EnumParameter<ParallelSide> HorizontalDirection = new(
        key: "parallel.horizontalDirection",
        @default: ParallelSide.Right);

    public EnumParameter<VerticalSide> VerticalDirection = new(
        key: "parallel.verticalDirection",
        @default: VerticalSide.Up);

    public BoolParameter ReverseDirection = new(
        key: "parallel.reverseDirection",
        @default: false);
}
```

### Tool definition example (Connect, with mode tags)

```csharp
public class ConnectTool : NT_ToolDefinition {
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
foreach (var tool in m_Tools) {
    foreach (var param in ReflectParameters(tool)) {
        RegisterBinding(param);
        param.OnChanged += () => MarkUpdateNeeded(tool);
    }
}

void RegisterBinding(ParameterBase param) {
    switch (param) {
        case FloatParameter f:
            var binding = CreateBinding(f.Key, f.Value, v => f.Value = v);
            f.OnChanged += () => binding.Value = f.Value;
            break;
        case EnumParameter<TEnum> e:
            // Cast to/from int for transport
            ...
        case Float3Parameter v3:
            // Use ValueWriter<float3> / ValueReader<float3>
            ...
    }
}
```

Reflection runs once per tool type at static init. `ParameterSchema.For(Type)` caches the discovered `ParameterBase[]` in a `Dictionary<Type, ParameterBase[]>` keyed by tool type. Subsequent lookups (binding setup, reset, codegen) hit the cache. No reflection on hot paths.

### Reset triggers

Expose two triggers for the UI:

```csharp
CreateTrigger<string>("RESET_PARAM", key => CurrentTool?.Reset(key));
CreateTrigger("RESET_TOOL",          ()  => CurrentTool?.ResetAll());
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

A single `.generated.ts` file emitted into `UI/src/` by the C# build (Roslyn source generator OR an MSBuild task that runs a small C# console tool).

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

Two viable paths:

1. **Roslyn incremental generator** — runs inside `dotnet build`. Cleanest. Steeper learning curve.
2. **MSBuild PreBuild task** — a small console exe that loads the compiled assembly, reflects, writes the `.ts` file. Easier to write, requires assembly to compile first (chicken/egg avoided by emitting only on the second pass or by parsing source instead of loading the assembly).

Recommend starting with option 2 (MSBuild task) — lower barrier, easy to iterate. Migrate to a Roslyn generator later if build-pipeline complexity warrants it.

---

## 9. Migration Plan

### Phase 1 — Foundation + Parallel migration (smallest viable first PR)

Ship the parameter system end-to-end against the simplest tool. Parallel has no modes, no float3, no complex handles — ideal validation target.

- Implement `ParameterBase`, `Parameter<T>`, and concrete subclasses (`Float`, `Int`, `Bool`, `Enum<T>`, `Float3`).
- Implement `ParameterSchema.For(Type)` with cached per-type reflection.
- Implement `NT_ToolDefinition` base with `ResetAll()` / `Reset(key)`.
- Implement reflection-driven binding registration in `NT_UISystem`.
- Implement generic handle drag dispatch using parameter refs (no `HandleKeys` switch).
- Convert `ParallelConfig` -> `ParallelTool : NT_ToolDefinition`.
- Replace `UpdateConfig(ParallelConfig)` with parameter-driven flow.
- Update `ParallelToolSystem` to build a `ParallelJobConfig` snapshot before scheduling.
- Add `RESET_PARAM` and `RESET_TOOL` triggers.
- Update the React Parallel panel to consume per-parameter bindings.

End-to-end: UI edit -> binding -> parameter -> snapshot -> job -> network. Burst job code unchanged.

### Phase 2 — Build-time TS codegen

- Add MSBuild task project that emits `parameters.generated.ts` (enums, `PARAM_KEYS`, `PARAM_META`)
- Wire into UI build so React picks it up.
- Convert the Parallel React panel to consume `PARAM_KEYS` and `PARAM_META` instead of hardcoded keys/ranges.

### Phase 3 — Migrate remaining tools

Order: Generate -> Connect -> RoadShape (increasing complexity).

For each:
- Convert config struct to `NT_ToolDefinition` subclass with parameter fields.
- Update generators to read from the snapshot struct (mostly mechanical rename).
- Replace `HandleKeys.X` references with parameter refs.
- Construct mode-specific handles inside generators (unchanged), but with parameter refs instead of keys.
- Drop the now-unused config struct.

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
   - Per-template "preset" methods on `RoadShapeTool` (e.g., `ApplySlopeLinearPreset()`), each calling `ResetAll()` then setting template-specific parameters, **or**
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
| New | `Systems/Parameters/Parameter.cs`, `FloatParameter.cs`, etc. |
| New | `Systems/Parameters/NT_ToolDefinition.cs` |
| New | MSBuild task project for TS codegen |
| New | `UI/src/parameters.generated.ts` |
| Modified | `Systems/UI/UISystem.cs` (reflection-driven binding setup) |
| Modified | `Systems/UI/UISystem.Handlers.cs` (generic handle dispatch) |
| Modified | `Systems/Tools/Parallel/*` (Phase 2) |
| Modified | `Systems/Tools/Generate/*`, `Connect/*`, `RoadShape/*` (Phase 4) |
| Deleted | `HandleKeys.cs` (Phase 5) |
| Deleted | Per-tool `*Config.cs` canonical structs (replaced by `*JobConfig.cs` snapshots) |
| Modified | `UI/src/gameBindings.ts` (drop duplicated types) |

---

## 12. Quick Reference: Before / After

| Concern | Today | After |
| --- | --- | --- |
| Parameter declaration | C# const + binding default + TS default + React slider props | One C# parameter field, codegen handles the rest |
| Serialization | Manual `Write` / `Read` per config | `Parameter<T>` base class |
| TS types | Hand-mirrored in `gameBindings.ts` | Build-time generated |
| UI binding | One per config struct | One per parameter |
| Handle -> field | `HandleKeys` enum + dispatch switch | Direct parameter reference |
| Mode dispatch in jobs | `switch (Mode)` -> generator | Unchanged (Burst-mandated) |
| Mode-specific handle shapes | Generator's `GetHandleDefinitions` switch | Unchanged |
| Burst job input | `Config` struct (canonical) | `JobConfig` struct (snapshot from parameters) |
| Change notification | Ad hoc + revision counters | `Parameter.OnChanged` events |
