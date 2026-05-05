# Generator API: Direct Parameter Seeding

### Context

Today's flow when a mode activates or context changes (Connect):

```csharp
// Tool builds a temporary JobConfig from current parameter values
var temp = new ConnectJobConfig {
    StartPosition  = StartPosition.Value,
    EndPosition    = EndPosition.Value,
    StartDirection = StartDirection.Value,
    EndDirection   = EndDirection.Value,
    // ... all the rest
};

// Generator populates mode-specific fields on the temp struct
generator.InitializeConfig(ref temp);

// Tool copies the populated values back to parameters
StartPosition.Value                  = temp.StartPosition;
CurveStartControlPointPosition.Value = temp.CurveStartControlPointPosition;
CurveEndControlPointPosition.Value   = temp.CurveEndControlPointPosition;
CurveStartPointPosition.Value        = temp.CurveStartPointPosition;
CurveEndPointPosition.Value          = temp.CurveEndPointPosition;
// ... ten more lines
```

Two-way data flow for what is conceptually one-way: "mode just changed → seed the mode-specific parameters from current geometry." The temp struct exists only to give the generator something Burst-shaped to write into, even though `InitializeConfig` is not a Burst job.

Same pattern likely exists in RoadShape's `IPathTransformation.Initialize` and Generate's mode-init paths. Confirm during implementation.

### Pain points

1. **Two-way copy boilerplate.** `parameter → temp → parameter` for every field involved in seeding. Every new mode-specific parameter adds two lines on each side.
2. **The temp struct lies.** It looks like a job snapshot but is used as an inout buffer for non-Burst code. Two distinct purposes wearing one name.
3. **Drift risk.** If a parameter is added to the JobConfig but the seeding copy-back is forgotten, the parameter silently stays at its old value while the generator thinks it set it.
4. **Burst constraint applied where it doesn't belong.** `InitializeConfig` runs in C# managed code at mode-change time. It can write `Float3Parameter.Value` directly. The struct shape is a habit, not a requirement.

### What is *not* a problem

- **`GenerateConnection` / `Process` / `GeneratePath` taking the snapshot struct.** That's the Burst entry point. Snapshot is genuinely required there.
- **The snapshot pattern itself.** Stays. See Part 2 for making it less manual.

### Proposed shape

Generators that do contextual seeding take the tool reference and write to parameters directly:

```csharp
public interface IConnectionGenerator {
    void SeedParameters(NT_ConnectToolSystem tool);
    void GenerateConnection(in ConnectJobConfig config, ref NativeList<CurveDef> curves);
}

// SimpleCurveGenerator implementation
public struct SimpleCurveGenerator : IConnectionGenerator {
    public void SeedParameters(NT_ConnectToolSystem tool) {
        var len = math.distance(tool.StartPosition.Value, tool.EndPosition.Value);
        tool.CurveStartPointPosition.Value        = tool.StartPosition.Value;
        tool.CurveEndPointPosition.Value          = tool.EndPosition.Value;
        tool.CurveStartControlPointPosition.Value = tool.StartPosition.Value + tool.StartDirection.Value * (len / 3);
        tool.CurveEndControlPointPosition.Value   = tool.EndPosition.Value   + tool.EndDirection.Value   * (len / 3);
    }

    public void GenerateConnection(in ConnectJobConfig config, ref NativeList<CurveDef> curves) {
        // unchanged
    }
}
```

Tool side:

```csharp
// Before:
//   var temp = BuildJobConfig();
//   generator.InitializeConfig(ref temp);
//   <copy temp.* back to parameters>
//
// After:
generator.SeedParameters(this);
```

The temp struct disappears. Parameters fire `OnChanged` as they're written, which (after the parameter refactor) automatically triggers UI binding pushes, handle reverse-sync, and `m_UpdateNeeded`.

### Side-effect ordering caveat

Writing parameters one at a time fires `OnChanged` per write. If a downstream subscriber observes intermediate state (e.g., `CurveStartControlPointPosition` before `CurveEndControlPointPosition` is set), it may briefly see inconsistent geometry.

Mitigations, in order of preference:

1. **Order writes outermost-first** so intermediate states are still geometrically valid (start anchor before its control point, etc.). Often sufficient.
2. **Add a batch-write scope on `NT_BaseToolSystem`:**

   ```csharp
   using (BeginParameterBatch()) {
       tool.CurveStartPointPosition.Value = ...;
       tool.CurveEndPointPosition.Value   = ...;
       // ... more writes
   } // OnChanged fires once at scope exit, per param that actually changed
   ```

   `Parameter<T>.Value` setter checks an ambient "batching" flag; when set, it stores the new value but defers `OnChanged`. The disposable scope flushes pending changes on exit. This generalizes beyond seeding (any multi-write operation benefits).

   Decide between (1) and (2) during implementation. (2) is more general but adds a piece of base-system machinery; (1) is free if write order is naturally safe.

### Migration plan

| Phase | Scope |
| --- | --- |
| 1 | Add `SeedParameters(NT_ConnectToolSystem)` to `IConnectionGenerator`. Implement on Connect's three generators. Update tool's mode-change path to call `SeedParameters` instead of the temp-struct dance. Delete `InitializeConfig` from `IConnectionGenerator`. Decide write-order vs batch scope based on observed UI flicker. |
| 2 | Same shape for Generate's `IGenerator.InitializeConfig` (if used) → `SeedParameters(NT_GenerateToolSystem)`. |
| 3 | Same shape for RoadShape's `IPathTransformation` if it has equivalent contextual init. |
| 4 | Remove temp `JobConfig` construction from tool seed paths. (`JobConfig` is still built normally for actual job scheduling — Part 2 streamlines that.) |

### Files likely to change

| Area | Files |
| --- | --- |
| Modified | `Systems/Tools/Connect/Core/IConnectionGenerator.cs` (replace `InitializeConfig` with `SeedParameters`) |
| Modified | `Systems/Tools/Connect/Generators/SimpleCurveGenerator.cs`, `LoopGenerator.cs`, `ComplexCurveGenerator.cs` |
| Modified | `Systems/Tools/Connect/ConnectToolSystem.Update.cs` (or wherever mode change triggers seeding) |
| Modified (Phase 2) | `Systems/Tools/Generate/Core/IGenerator.cs`, generators, tool system |
| Modified (Phase 3) | `Systems/Tools/RoadShape/Core/IPathTransformation.cs`, transforms, tool system |
| New (optional) | `Systems/Tools/Parameters/ParameterBatchScope.cs` (if batch-write scope chosen over write-order discipline) |

### Open questions

1. **Should `SeedParameters` be allowed to read other generators' state?** No — keep it strictly "this generator seeds its own mode's parameters from currently-set shared parameters." If cross-mode state is needed, hoist to the tool.
2. **Does Generate's contextual init follow the same shape?** Confirm during Phase 2 by reading `GenerateToolSystem.Update.cs` and the generators. If Generate doesn't have contextual seeding, skip its phase entirely.
3. **Batch-write scope nesting / threading.** If introduced, decide whether nested scopes flush at the outermost exit only. Single-threaded by tool-system contract, so no thread-safety needed.

