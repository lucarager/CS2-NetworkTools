# Path Transform Refactoring - Cleanup Tracking

This document tracks cleanup and refactoring items identified after the initial Shape & Slope unification refactor.

---

## Status Legend

| Status | Meaning |
|--------|---------|
| ? | Not started |
| ?? | In progress |
| ? | Complete |
| ? | Cancelled/Won't do |

---

## Priority 1: Remove Obsolete Types & Consolidate

These items block future work and should be addressed first.

| # | Status | Item | Description | Files Affected |
|---|--------|------|-------------|----------------|
| 1.1 | ? | Remove `EdgeSlopeData` | Obsolete - replaced by `EdgeTransformState` | `SlopeCalculator.cs` |
| 1.2 | ? | Remove `ComputedEdgeSlope` | Obsolete - replaced by `EdgeTransformState` | `SlopeCalculator.cs` |
| 1.3 | ? | Consolidate `CalculateControlPointRatios` | Exists in both `SlopeCalculator` and `ShapeCalculator`. Move to shared location. | `SlopeCalculator.cs`, `ShapeCalculator.cs`, `EdgeTransformState.cs` |
| 1.4 | ? | Fix inconsistent ratio calculation call | `GatherEdgeStates` calls `SlopeCalculator.CalculateControlPointRatios`, but `EdgeTransformState.RecalculateControlPointRatios()` calls `ShapeCalculator`. Should use one source. | `NT_SlopeToolSystem.Jobs.cs`, `EdgeTransformState.cs` |

### Proposed Solution for 1.3 & 1.4

Move `CalculateControlPointRatios` to `EdgeTransformState` as a static method or create a new `BezierUtils` class:

```csharp
// Option A: On EdgeTransformState
public static void CalculateControlPointRatios(in Bezier4x3 bezier, float length, bool isForward, out float ctrlStart, out float ctrlEnd)

// Option B: New BezierUtils class
public static class BezierUtils {
    public static void CalculateControlPointRatios(...) { }
}
```

---

## Priority 2: API Consistency

Make slope transforms follow the same pattern as shape transforms.

| # | Status | Item | Description | Files Affected |
|---|--------|------|-------------|----------------|
| 2.1 | ? | Create slope-specific transform methods | Add `ApplyLinearTransform()`, `ApplyEaseInOutTransform()`, `ApplyParabolicTransform()` | `NT_SlopeToolSystem.Jobs.cs` |
| 2.2 | ? | Update `ApplySlopeTransforms` to use switch | Match the pattern in `ApplyShapeTransforms` | `NT_SlopeToolSystem.Jobs.cs` |
| 2.3 | ? | Move config handling into transform methods | Each method handles its own config parameters, not passed from caller | `NT_SlopeToolSystem.Jobs.cs`, `SlopeCalculator.cs` |

### Target Pattern

```csharp
private void ApplySlopeTransforms(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
    if (!ctx.Config.HasSlopeTransform) return;

    switch (ctx.Config.Slope.Template) {
        case SlopeTemplate.Linear:
            ApplyLinearSlopeTransform(edges, in ctx);
            break;
        case SlopeTemplate.EaseInOut:
            ApplyEaseInOutSlopeTransform(edges, in ctx);
            break;
        case SlopeTemplate.Parabolic:
            ApplyParabolicSlopeTransform(edges, in ctx);
            break;
    }
}
```

---

## Priority 3: Reduce Parameter Counts

Methods with many parameters are hard to read and maintain.

| # | Status | Item | Description | Current Params | Target |
|---|--------|------|-------------|----------------|--------|
| 3.1 | ? | `SlopeCalculator.CalculateEdgeHeights` | Takes 8 parameters | 8 | Use `EdgeTransformState` + `TransformContext` |
| 3.2 | ? | `ShapeCalculator.CalculateSmoothedPositions` | Takes 12 parameters! | 12 | Use structs or context |
| 3.3 | ? | `ShapeCalculator.CalculateStraightenedPositions` | Takes 7 parameters | 7 | Use `EdgeTransformState` + `TransformContext` |

### Proposed Approach

Create overloads that accept `EdgeTransformState` and `TransformContext`:

```csharp
// Before
var heights = SlopeCalculator.CalculateEdgeHeights(
    state.CumulativeDistance, state.Length, state.CtrlStartRatio, state.CtrlEndRatio,
    ctx.TotalLength, ctx.StartHeight, ctx.DeltaHeight, ctx.Config.Slope);

// After
var heights = SlopeCalculator.CalculateEdgeHeights(state, ctx);
```

---

## Priority 4: Intersection Handling Improvements

| # | Status | Item | Description | Files Affected |
|---|--------|------|-------------|----------------|
| 4.1 | ? | Support all shape templates for XZ delta | Currently only `Straighten` calculates XZ deltas. `Smooth` is ignored. | `NT_SlopeToolSystem.Jobs.cs` |
| 4.2 | ? | Extract threshold constants | `0.001f` and `0.000001f` are magic numbers | `NT_SlopeToolSystem.Jobs.cs` |
| 4.3 | ? | Store transformed node positions | Currently recalculates new positions in intersection handling. Could store them during transform phase. | `EdgeTransformState.cs`, `NT_SlopeToolSystem.Jobs.cs` |

### Code Location for 4.1

```csharp
// Line ~365 in NT_SlopeToolSystem.Jobs.cs
// Currently:
if (ctx.Config.HasShapeTransform && ctx.Config.Shape.Template == ShapeTemplate.Straighten) {

// Should support:
if (ctx.Config.HasShapeTransform) {
    // Calculate newXZ based on template type
}
```

---

## Priority 5: Code Organization & Naming

| # | Status | Item | Description | Files Affected |
|---|--------|------|-------------|----------------|
| 5.1 | ? | Rename `SlopeCalculator` | Name is misleading now. Consider `HeightCalculator` or merge into unified class. | `SlopeCalculator.cs` |
| 5.2 | ? | Update `ShapeCalculator` comment | Says "Mirrors SlopeCalculator structure" - outdated | `ShapeCalculator.cs` |
| 5.3 | ? | Split data structs from calculators | `SlopeCalculator.cs` contains structs AND the calculator. Consider separate files. | `SlopeCalculator.cs` |

---

## Priority 6: Minor Improvements

| # | Status | Item | Description | Files Affected |
|---|--------|------|-------------|----------------|
| 6.1 | ? | Add `TransformContext.Create()` validation | No check for invalid positions or null nodes | `EdgeTransformState.cs` |
| 6.2 | ? | Cache transformed length on `EdgeTransformState` | Currently recalculated via `MathUtils.Length()` in multiple places | `EdgeTransformState.cs`, `NT_SlopeToolSystem.Jobs.cs` |
| 6.3 | ? | Add `IsValid` check to `EdgeTransformState` | Could validate entity exists, length > 0, etc. | `EdgeTransformState.cs` |

---

## Implementation Plan

### Phase 1: Cleanup (Low Risk)
- Items 1.1, 1.2 - Remove obsolete types
- Items 5.2 - Update comments
- Item 4.2 - Extract constants

### Phase 2: Consolidation (Medium Risk)
- Items 1.3, 1.4 - Consolidate `CalculateControlPointRatios`
- Item 5.3 - Split files

### Phase 3: API Harmonization (Medium Risk)
- Items 2.1, 2.2, 2.3 - Slope transform methods
- Items 3.1, 3.2, 3.3 - Reduce parameter counts

### Phase 4: Feature Improvements (Higher Risk)
- Items 4.1, 4.3 - Intersection handling improvements

### Phase 5: Naming (Breaking Changes)
- Item 5.1 - Rename `SlopeCalculator` (may affect other files)

---

## Notes

- All changes should maintain backward compatibility with the UI bindings
- Run full test suite after each phase
- Consider adding unit tests for calculators before refactoring them

---

## Related Documents

- [REFACTOR_SHAPE_AND_SLOPE_TOOL.md](./REFACTOR_SHAPE_AND_SLOPE_TOOL.md) - Original refactoring plan
