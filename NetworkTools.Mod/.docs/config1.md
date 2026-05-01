# Config Refactor Plan: Reflection-Driven Serialization

## Background

The mod currently has 4 "Config" structs that hold tool parameters:

- `ParallelConfig` (Systems/Tools/Parallel/Core/) — fully serialized
- `ShapeTransformConfig` (Systems/Tools/RoadShape/Core/) — fully serialized, has forward-compat conditional reads
- `ConnectConfig` (Systems/Tools/Connect/Core/) — placeholder Write/Read (no real serialization)
- `GenerateConfig` (Systems/Tools/Generate/Core/) — placeholder Write/Read

These configs flow through:

1. **UI bindings** in `NT_UISystem` (UI/UISystem.cs) — TwoWayBinding<TConfig> with manual ValueWriter/ValueReader
2. **Tool systems** — store `CurrentConfig`, expose `UpdateConfig`/`SetMode`, mark `m_UpdateNeeded`
3. **Burst jobs** — read config to drive curve/network/transform generation
4. **Handles** — drag operations write back to config fields via `switch (key)` blocks
5. **TS bindings** in `UI/src/gameBindings.ts` — manually mirrored types & defaults

## Problem

Per-config boilerplate proliferates:

- Each config implements `Write`/`Read` by hand — error-prone and two are placeholders
- Each tool's handle handlers contain `switch (handleKey)` to map keys to fields
- C# field names, JSON property names, and TS types are kept in sync manually
- Adding a new field to any config requires touching ≥3 files

## Goal

Replace per-config manual serialization with **attribute-driven, reflection-built** infrastructure. Configs declare their fields once with `[ConfigParam]`; serialization, defaults, and (later) handle dispatch derive from those declarations.

## Constraint: Burst Boundary

Configs must remain Burst-compatible structs. **Reflection only runs on the main thread** (binding handlers, startup). Inside Burst-compiled jobs:

- The mode/template `switch` statements stay (e.g. `switch (Mode) { case ConnectMode.SimpleCurve: ... }`). These are Burst's required dispatch mechanism — not part of this refactor.
- The generator interfaces (`IConnectionGenerator`, `IGenerator`, `IPathTransformation`) stay as-is.

## Out of Scope

1. **TypeScript type generation** — deferred to a separate project. The attribute schema being defined here will eventually drive a build-time TS emitter.
2. **Burst job switch statements** — unavoidable; unchanged.
3. **Tool-system → job flow** (`m_UpdateNeeded`, revision counters) — works fine.
4. **Config struct memory layout** — fields stay in current order/types so Burst code is untouched.

## Resolved Design Decisions

1. **`float3` JSON shape:** nested `{ "x": .., "y": .., "z": .. }` object (Unity convention).
2. **Defaults:** support both per-field attribute defaults AND a static `Default` property — they serve different purposes.
   - Attribute default → drives single-field reset (future "reset this slider" feature)
   - Static `Default` → drives full-struct init at tool startup; can derive from attribute defaults where contextual init isn't needed
3. **Handle-key bridging:** separate refactor, not in this plan.
4. **`float3` attribute defaults:** skip them. Attributes only allow primitive constants, and float3 fields are almost always positions/directions populated from selected nodes — contextual init via static `Default(args)` is the right pattern.

---

## Architecture

```
[ConfigParam] attribute on struct fields
         ↓
ConfigSchema<T>  — reflected once per type at static-init,
                   caches FieldDescriptor[] + key lookup
         ↓
ITypeHandler registry — one handler per supported field type
         ↓
ConfigSerializer.Write/Read<T>  — generic, drives any config struct
ConfigSerializer.ResetField<T>  — single-field reset from attribute default
ConfigSerializer.ResetAllDeclaredDefaults<T> — bulk reset of attributed fields
```

Supported field types (Phase 1):

- `float`, `int`, `bool`
- `float3` (nested {x,y,z})
- any `enum` (cast to int)

---

## Pseudocode

### 1. Attribute

```csharp
[AttributeUsage(AttributeTargets.Field)]
public sealed class ConfigParamAttribute : Attribute {
    public string Key;                      // JSON property name
    public float  Min     = float.MinValue;
    public float  Max     = float.MaxValue;
    public object Default = null;           // optional; null = no per-field default
}
```

The `object Default` is boxed once at attribute construction; never on the hot path.

### 2. Type handler interface

```csharp
public unsafe interface ITypeHandler {
    void Write     (IJsonWriter writer, void* fieldPtr);
    void Read      (IJsonReader reader, void* fieldPtr);
    void WriteBoxed(void* fieldPtr, object value);   // for reset-from-attribute
}
```

Concrete handlers:

```csharp
class FloatHandler  : ITypeHandler { /* *(float*)ptr  */ }
class IntHandler    : ITypeHandler { /* *(int*)ptr    */ }
class BoolHandler   : ITypeHandler { /* *(bool*)ptr   */ }

class Float3Handler : ITypeHandler {
    public void Write(IJsonWriter w, void* p) {
        var v = *(float3*)p;
        w.TypeBegin("Unity.Mathematics.float3");
        w.PropertyName("x"); w.Write(v.x);
        w.PropertyName("y"); w.Write(v.y);
        w.PropertyName("z"); w.Write(v.z);
        w.TypeEnd();
    }
    public void Read(IJsonReader r, void* p) {
        r.ReadMapBegin();
        r.ReadProperty("x"); r.Read(out float x);
        r.ReadProperty("y"); r.Read(out float y);
        r.ReadProperty("z"); r.Read(out float z);
        r.ReadMapEnd();
        *(float3*)p = new float3(x, y, z);
    }
    public void WriteBoxed(void* p, object value) {
        // Not supported for float3 — see Resolved Decisions #4
        throw new NotSupportedException("float3 attribute defaults are not supported");
    }
}

// Generic enum handler, cached per concrete enum type via MakeGenericType
class EnumHandler<TEnum> : ITypeHandler where TEnum : unmanaged, Enum { /* cast to int */ }
```

### 3. Type handler registry

```csharp
public static class TypeHandlerRegistry {
    static Dictionary<Type, ITypeHandler> s_Handlers = new() {
        { typeof(float),  new FloatHandler()  },
        { typeof(int),    new IntHandler()    },
        { typeof(bool),   new BoolHandler()   },
        { typeof(float3), new Float3Handler() },
    };

    public static ITypeHandler Resolve(Type fieldType) {
        if (s_Handlers.TryGetValue(fieldType, out var h)) return h;
        if (fieldType.IsEnum) {
            var generic = typeof(EnumHandler<>).MakeGenericType(fieldType);
            var inst    = (ITypeHandler)Activator.CreateInstance(generic);
            s_Handlers[fieldType] = inst;
            return inst;
        }
        throw new NotSupportedException($"No handler for {fieldType}");
    }
}
```

### 4. Field descriptor + schema cache

```csharp
public readonly struct FieldDescriptor {
    public readonly string       Key;
    public readonly int          Offset;    // from UnsafeUtility.GetFieldOffset
    public readonly ITypeHandler Handler;
    public readonly object       Default;   // boxed; null if not declared
}

public static class ConfigSchema<T> where T : struct {
    public static readonly FieldDescriptor[]                  Fields;
    public static readonly Dictionary<string, FieldDescriptor> FieldsByKey;

    static ConfigSchema() {
        var list = new List<FieldDescriptor>();
        foreach (var f in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            var attr = f.GetCustomAttribute<ConfigParamAttribute>();
            if (attr == null) continue;
            list.Add(new FieldDescriptor(
                key:     attr.Key,
                offset:  UnsafeUtility.GetFieldOffset(f),
                handler: TypeHandlerRegistry.Resolve(f.FieldType),
                @default: attr.Default
            ));
        }
        Fields      = list.ToArray();
        FieldsByKey = list.ToDictionary(d => d.Key);
    }
}
```

### 5. Generic serializer + reset

```csharp
public static class ConfigSerializer {
    public static unsafe void Write<T>(IJsonWriter writer, ref T config) where T : struct {
        writer.TypeBegin(typeof(T).FullName);
        fixed (void* basePtr = &config) {
            foreach (var field in ConfigSchema<T>.Fields) {
                writer.PropertyName(field.Key);
                field.Handler.Write(writer, (byte*)basePtr + field.Offset);
            }
        }
        writer.TypeEnd();
    }

    public static unsafe void Read<T>(IJsonReader reader, ref T config) where T : struct {
        reader.ReadMapBegin();
        fixed (void* basePtr = &config) {
            // Tolerate missing/extra keys for forward compat
            while (reader.HasNextProperty(out var name)) {
                if (ConfigSchema<T>.FieldsByKey.TryGetValue(name, out var field)) {
                    field.Handler.Read(reader, (byte*)basePtr + field.Offset);
                } else {
                    reader.SkipValue();
                }
            }
        }
        reader.ReadMapEnd();
    }

    public static unsafe bool ResetField<T>(ref T config, string key) where T : struct {
        if (!ConfigSchema<T>.FieldsByKey.TryGetValue(key, out var field)) return false;
        if (field.Default == null) return false;
        fixed (void* basePtr = &config) {
            field.Handler.WriteBoxed((byte*)basePtr + field.Offset, field.Default);
        }
        return true;
    }

    public static unsafe void ResetAllDeclaredDefaults<T>(ref T config) where T : struct {
        fixed (void* basePtr = &config) {
            foreach (var field in ConfigSchema<T>.Fields) {
                if (field.Default == null) continue;
                field.Handler.WriteBoxed((byte*)basePtr + field.Offset, field.Default);
            }
        }
    }
}
```

### 6. Migrated config example: ParallelConfig

```csharp
public struct ParallelConfig : IJsonWritable, IJsonReadable {
    [ConfigParam("horizontalOffset",    Default = 20f,  Min = 0f, Max = 80f)]
    public float HorizontalOffset;

    [ConfigParam("verticalOffset",      Default = 20f,  Min = 0f, Max = 80f)]
    public float VerticalOffset;

    [ConfigParam("horizontalDirection", Default = (int)ParallelSide.Right)]
    public ParallelSide HorizontalDirection;

    [ConfigParam("verticalDirection",   Default = (int)VerticalSide.Up)]
    public VerticalSide VerticalDirection;

    [ConfigParam("reverseDirection",    Default = false)]
    public bool ReverseDirection;

    public static ParallelConfig Default {
        get {
            var c = new ParallelConfig();
            ConfigSerializer.ResetAllDeclaredDefaults(ref c);
            return c;
        }
    }

    public void Write(IJsonWriter w) => ConfigSerializer.Write(w, ref this);
    public void Read (IJsonReader r) => ConfigSerializer.Read (r, ref this);

    // Domain helpers stay
    public float SignedHorizontalOffset => HorizontalDirection == ParallelSide.Right ?  HorizontalOffset : -HorizontalOffset;
    public float SignedVerticalOffset   => VerticalDirection   == VerticalSide.Up    ?  VerticalOffset   : -VerticalOffset;
}
```

For configs where the static `Default` needs context (Connect's start/end positions, Generate's spawn point), keep a hand-written `Default(args)` that calls `ResetAllDeclaredDefaults` and then sets the float3 fields from arguments.

---

## Phased Migration Plan

### Phase 0 — Setup

- Create folder `Systems/Tools/Common/Config/`
- Lock the `ConfigParamAttribute` API
- Lock the `ITypeHandler` API
- **No config files touched yet**

### Phase 1 — Infrastructure

- Implement `ITypeHandler` + concrete handlers (`Float`, `Int`, `Bool`, `Float3`, generic `Enum<TEnum>`)
- Implement `TypeHandlerRegistry` with enum `MakeGenericType` caching
- Implement `ConfigSchema<T>` with reflection at static-init
- Implement `ConfigSerializer.Write/Read<T>`, `ResetField<T>`, `ResetAllDeclaredDefaults<T>`
- Unit tests against a synthetic `TestConfig` covering: float, int, bool, float3, enum, missing-field forward-compat, reset
- **No production configs touched yet**

### Phase 2 — Migrate `ParallelConfig` (lowest risk; already fully serialized)

- Add attributes; replace Write/Read with delegating calls
- Snapshot-test JSON output to prove byte-identical to pre-refactor
- Unit-test per-field reset
- Keep JSON property names exactly so existing UI bindings continue to work

### Phase 3 — Migrate `ShapeTransformConfig` (most complex existing serializer)

- Add attributes for all 6 fields plus `RenderSlopeTooltips`
- Verify forward-compat: `ConfigSerializer.Read` must skip unknown keys and tolerate missing keys (matches existing conditional-read pattern)
- Snapshot-test JSON output
- Verify `ShapeConfigRevision` flow still triggers UI sync

### Phase 4 — Migrate `ConnectConfig` (gain: actual serialization)

- This is the main `float3` test case (StartPosition, EndPosition, StartDirection, EndDirection, CurveStart/EndPoint, CurveStart/EndControlPoint, LoopControlPoint)
- Define JSON keys: `startPosition`, `endPosition`, `startDirection`, `endDirection`, `curveStartPointPosition`, `curveStartControlPointPosition`, `curveEndControlPointPosition`, `curveEndPointPosition`, `loopControlPointPosition`, `loopRadius`
- Coordinate `gameBindings.ts` — add a real `ConnectConfigData` type matching the JSON shape (manual for now; future TS-gen project will automate)
- Document JSON shape in this folder for the TS-gen project to consume

### Phase 5 — Migrate `GenerateConfig`

- Mix of float3 (StartPosition, StartDirection) and grid scalars (GridXSpacing, GridZSpacing, GridXNum, GridZNum)
- Update `gameBindings.ts` `GenerateConfigData` to match real serialization
- Snapshot-test JSON output

### Phase 6 — (Deferred) Handle-key bridge

Tracked separately. Not part of this refactor.

---

## Risk Register

| Risk | Mitigation |
|---|---|
| Reflection at startup adds load time | Static-init runs once per type; tiny cost. Measure if concerned. |
| `unsafe` + `UnsafeUtility.GetFieldOffset` correctness | Wrap in well-tested handlers; cross-check against `Marshal.OffsetOf` in tests |
| Burst inadvertently sees reflection code | Serializer lives in main-thread-only namespace; jobs never call it. Code review at PR boundaries. |
| JSON output shape changes break existing UI bindings | Snapshot-test JSON before/after migration per config. Keep `Key` strings identical. |
| Forward-compat regression in `ShapeTransformConfig` | `Read` skips unknown keys, tolerates missing keys (matches current behavior) |
| `float3` JSON shape disagreement with TS side | `{ "x", "y", "z" }` documented and matched in `gameBindings.ts` |
| Boxing in `WriteBoxed` adds GC pressure | Only invoked on user-initiated reset (rare); not on hot path |

---

## Acceptance Criteria

- [ ] All 4 configs use attributes + delegating Write/Read; no per-field manual code
- [ ] Snapshot tests prove JSON output for `ParallelConfig` and `ShapeTransformConfig` is byte-identical to pre-refactor
- [ ] `ConnectConfig` and `GenerateConfig` actually serialize (no more placeholder)
- [ ] `float3`, `float`, `int`, `bool`, and enum fields all round-trip through Write→Read
- [ ] `ResetField<T>` works for at least one config (covered by ParallelConfig tests)
- [ ] Adding a new field to any config requires only: declare field + attribute. Nothing else in C#.
- [ ] No reflection or `unsafe` code reachable from any `[BurstCompile]` job

---

## Things Explicitly NOT Changing

1. Burst job switches (`switch (Mode) { case ConnectMode.SimpleCurve: ... }`)
2. Generator interfaces (`IConnectionGenerator`, `IGenerator`, `IPathTransformation`)
3. Tool-system → job flow (`m_UpdateNeeded`, revision counters)
4. TypeScript bindings (still hand-maintained; deferred to TS-gen project)
5. Config struct field layout / order / types

---

## Future Work (Tracked Separately)

- **TS type generator** — build-time tool that reflects the C# assembly and emits `gameBindings.ts` types + defaults
- **Handle-key bridge** — `[HandleKey(int)]` attribute alongside `[ConfigParam]`; replace `switch (key)` blocks in handle handlers with generic dispatcher
- **Per-field reset UI** — surface `ResetField<T>` to React via a new trigger; per-slider reset buttons
- **Validation from `Min`/`Max`** — currently advisory; could be enforced in `Read` or in handle drag clamps

---

## For the Next Agent (Onboarding)

### Read These First (in order)

1. `.agents/instructions.md` — project coding conventions (namespaces, naming, formatting, ECS patterns). Follow these strictly.
2. `Systems/Tools/Parallel/Core/ParallelConfig.cs` — simplest existing config, the migration template.
3. `Systems/Tools/RoadShape/Core/ShapeTransformConfig.cs` — most complex existing serializer; the forward-compat conditional reads must be preserved by the new `ConfigSerializer.Read`.
4. `Systems/UI/UISystem.cs` lines ~96–124 — see how configs are wired to bindings via `CreateBinding(..., new ValueWriter<T>(), new ValueReader<T>())`. The new generic Write/Read must integrate cleanly with `ValueWriter<T>`/`ValueReader<T>`.
5. `Systems/UI/UISystem.Handlers.cs` — see `HandleUpdateParallelConfig`, `HandleUpdateShapeConfig`, etc. These call back into tool systems' `UpdateConfig`/`SetTransformationConfig`.

### Path Index

| Topic | Location |
|---|---|
| Existing configs | `Systems/Tools/{Parallel,Connect,Generate,RoadShape}/Core/*Config.cs` |
| New infra (to create) | `Systems/Tools/Common/Config/` |
| UI bindings | `Systems/UI/UISystem.cs` + `UISystem.Handlers.cs` + `UISystem.Update.cs` |
| TS bindings (do not auto-generate yet) | `UI/src/gameBindings.ts` |
| Tool systems (consumers) | `Systems/Tools/*/...ToolSystem*.cs` |
| Coding conventions | `.agents/instructions.md` |
| This plan | `.docs/config-refactor-plan.md` |

### API Verification Needed Before Phase 1

The pseudocode uses `reader.HasNextProperty(out var name)` and `reader.SkipValue()` for forward-compat reads. These names are **plausible but unverified** against Colossal's `IJsonReader` API. **First task in Phase 1: open one of the existing `Read()` implementations (e.g. `ShapeTransformConfig.Read`) and confirm the actual member names**, then update `ConfigSerializer.Read` to use the real API. Likely candidates: `ReadProperty`, `Skip`, `IsAtMapEnd`, or similar.

### Build / Compile

- **Project:** `NetworkTools.csproj` (.NET Framework 4.8, C# 9.0)
- **Build:** Open in IDE or `dotnet build` / `msbuild` from the project directory.
- **No existing test project** — if added during Phase 1, place under `NetworkTools.Tests/` as a sibling project with NUnit or xUnit. Coordinate with the user before adding test infrastructure; an alternative is one-off "verification programs" run from the editor.

### Validation Strategy Per Phase

- **Phase 1 (infrastructure):** Unit tests on synthetic `TestConfig`. No game launch needed.
- **Phase 2 (ParallelConfig):** Snapshot test JSON output. Manual smoke test: open Parallel tool in-game, change offset/side via UI, confirm preview updates correctly. Save/reload game and confirm config persists.
- **Phase 3 (ShapeTransformConfig):** Same snapshot + smoke test. Critically, test forward-compat by hand-editing a save's serialized config to remove a property, then loading.
- **Phase 4–5 (Connect/Generate):** These currently don't serialize, so there's nothing to compare against. Smoke test: change config in UI, save, reload, confirm config restored. Round-trip through Write→Read in a unit test.

### Coding Conventions Recap (from `.agents/instructions.md`)

- 4-space indent, opening brace on same line
- `NT_` prefix for systems/components
- `m_` prefix for private fields
- Aligned multi-field declarations where it improves readability
- `using` statements **inside** the namespace block
- XML doc comments on public APIs
- `PrefixedLogger` for logging (`m_Log = new PrefixedLogger(nameof(MySystem));`)

### Smallest Viable First PR

Phase 0 + the API verification step above + an empty `ConfigParamAttribute.cs` and `ITypeHandler.cs`. This unblocks the rest without touching any production config. Get this reviewed before building out Phase 1 in full.

### Important Gotchas

- **Static `Default` callers exist** — search for `ParallelConfig.Default` and `ShapeTransformConfig.Preserve()`/`SlopeLinear()`/etc. callers before changing those APIs. The factory methods on `ShapeTransformConfig` are called from `UISystem.Handlers.HandleUpdateShapeConfig` switch — those need to keep working.
- **`UnsafeUtility.GetFieldOffset`** lives in `Unity.Collections.LowLevel.Unsafe`. May require `unsafe` keyword on containing methods and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the csproj — verify this is already enabled (it likely is, given Burst usage).
- **`m_UpdateNeeded` flag** on tool systems must still be set by the binding handlers after `UpdateConfig` — don't break that flow.
- **`ShapeConfigRevision`** counter on `NT_RoadShapeToolSystem` must still increment whenever the config is mutated, otherwise UI sync breaks (see `UISystem.Update.cs` lines ~122–127).
- **Don't change JSON property names** during migration. The TS side reads by name; renaming silently breaks the UI binding without a compile error. Snapshot tests catch this.

### Questions to Ask the User Before Starting

1. Is there an existing test project, or should one be added now?
2. Are there any in-flight branches that touch these config files? (avoid merge conflicts)
3. Confirm: float3 JSON shape `{x,y,z}` is acceptable to whatever future TS-gen project consumes it?
