# NetworkTools Tool-System Refactor Plan

Execution plan distilled from the architecture review. For evidence, exact line numbers, and the
findings that were considered and **rejected**, see `CODE_REVIEW_ToolSystems.md` (referenced as
*Review NT-XXX* below). This document carries only the work that should actually happen.

**Goal:** retire accumulated duplication and tighten a few fragile base-class contracts, *without*
a structural rewrite. The modern abstractions are sound — this is a sequence of behavior-preserving
extractions on a stable base, preceded by one correctness fix.

**Scope**
- **In:** the five tool systems `NT_BaseToolSystem`, `NT_PathSelectionToolSystem`,
  `NT_RoadShapeToolSystem`, `NT_ParallelToolSystem`, `NT_GenerateToolSystem`.
- **Out:** rewriting the parameter/handle/snap/transform subsystems (keep them — see *Preserve*);
  the AddNode/RemoveNode/SlideNode/SuperNode/Connect tools except where a base change propagates to
  them mechanically; and **implementing the `SlopeArch`/`CurveSmooth` transforms** (a future effort —
  keep their scaffolding, see NT-008).

---

## Ground rules (read before editing)

- **Build via the solution** so `$(SolutionDir)` resolves: `dotnet build CS2-NetworkTools.sln`.
  Do **not** build `NetworkTools.Mod\NetworkTools.csproj` directly.
- **Partial-class rule:** before changing any system, read *all* its partials
  (`*.cs`, `.Lifecycle.cs`, `.Update.cs`, `.Jobs.cs`, `.JobMethods.cs`). A method that looks
  misplaced in one partial is often explained by a sibling.
- Conventions in `CLAUDE.md`: `NT_`/`m_` prefixes, prefer `Colossal.Mathematics.MathUtils` over
  custom math, every UI string is an l10n key.
- Work on a branch off `main`; commit only when asked.
- Most of this is **behavior-preserving** — the bar is "build clean + no behavior change." The two
  exceptions (Phase 0 correctness, NT-016 performance) need the in-game checks noted on each.
- After each phase, build and confirm no regression before starting the next.

## Preserve (do not discard while refactoring)

The declarative parameter system (`ParameterBase`/`Parameter<T>`, `OnChanged`, JSON persistence);
the spec-driven handle system (`IHandleSpec` + `RebuildHandlesForActiveMode` + drag→`SetValue`);
the `IPathTransformation` + `TransformPipeline.Execute<T>` strategy; the ported `SnapPlacementJob`;
`OperationPhase` + the `CreationDefinition`/`NetCourse` temp pipeline; the PathSelection state
machine + Dijkstra search; `EdgeConfig` as the cross-tool edge carrier. **Per-tool `Apply()`
semantics differ on purpose** (Parallel re-applies → `Phase=Ready`; RoadShape/Generate are one-shot
→ `ResetToIdle`) — do not force-unify them.

---

## Phase 0 — Correctness (ship and verify before any refactor) · ✅ Done (2026-06-12)

### NT-011 · High · handle parent-follow skips Axis/Position children · ✅ Done
- **✅ Done (2026-06-12):** shipped with NT-012. Behavior fix — verified via in-game repro
  (Generate → Oval → drag Position handle), not unit-tested.
- **Where:** `Base/BaseToolSystem.Handles.cs` `SyncParentPositionToChildHandles` (~469–485);
  reverse-sync subscriber in `Base/BaseToolSystem.Parameters.cs` (~91–96); manifests on
  `Generate/GenerateToolSystem.cs` `OvalRadiusZ` AxisHandle (~78–90, `Parent = nameof(Position)`).
- **Problem:** when a parent `Float3Parameter` moves, only Circle/Rotation child handles are
  repositioned; Axis/Position children parented to it are in neither sync path, so the oval radius
  handle lags the origin and drags along a stale axis.
- **Fix surface (confirmed by playtest):** the **parameter-value path already works** — a
  `Float3Parameter` child (e.g. the `OvalAxisPoint` PositionHandle) follows the parent, because
  `m_ParentChildLinks` shifts its value → its non-Handle `OnChanged` → reverse-sync moves its entity.
  The broken case is the **AxisHandle**, whose owning parameter is a `FloatParameter` (radius), so it
  is *not* in `m_ParentChildLinks` and its geometry (`StartPoint`/`EndPoint` delegates referencing
  `Position.Value`) is resolved only once at creation. **So fix the handle-entity side, not the
  value path:** when a parent `Float3Parameter` moves, re-resolve the AxisHandle's `GetAxisInfo` and
  rewrite its `NT_HandlePosition` + `NT_HandleConstraints` (the latter is NT-012).
- **Target:** extend the child-follow `switch` in `SyncParentPositionToChildHandles` to cover
  `AxisHandle` (and any `FloatParameter`/`ComputedPositionHandle` child whose geometry derives from a
  parent), repositioning the entity center and refreshing the constraint. Leave the working
  Float3Parameter value-delta path alone.
- **Verify:** Generate → Oval → place origin + axis point (reach Ready) → drag the Position handle;
  the OvalRadiusZ handle must track the new center and stay on the correct axis. Can't be
  unit-tested — provide repro steps, don't claim verified.

### NT-012 · Medium · constraints never refreshed by SyncToEntity (enables NT-011) · ✅ Done
- **✅ Done (2026-06-12):** shipped with NT-011. `AxisHandle.SyncToEntity` now rebuilds
  `NT_HandleConstraints` from the live endpoint delegates alongside the position.
- **Where:** `Handles/AxisHandle.cs` `SyncToEntity` (~46–50); constraint creation in
  `Base/BaseToolSystem.Handles.cs` `CreateHandleFromSpec` (~211–217) and
  `ResolvePositionConstraintFields` (~338–366).
- **Problem:** `SyncToEntity` writes only `NT_HandlePosition`; `NT_HandleConstraints` (axis/origin/
  bounds) are built once at creation and never updated, so a moved parent leaves the child bound to
  the old axis even if its center follows.
- **Target:** in `SyncToEntity` for axis/constrained-position handles, recompute and
  `SetComponentData` the `NT_HandleConstraints` from the spec's endpoint delegates alongside the
  position. Do this together with NT-011.

---

## Phase 1 — Base-contract changes (behavior-preserving) · ✅ Done (2026-06-12)

### NT-002 · PathSelection self-managing lifecycle · ✅ Done
- **✅ Done (2026-06-12):** `NT_PathSelectionToolSystem` now overrides the four lifecycle methods
  and self-manages init/reset/clear/dispose; the RoadShape/Parallel hand-calls are gone.
- **Where:** `PathSelection/PathSelectionToolSystem.Lifecycle.cs` (`InitializeSelectionState` 18,
  `DisposeSelectionState` 31); hand-calls in `RoadShape/…Lifecycle.cs` (71, 81, 103, 110) and
  `Parallel/…Lifecycle.cs` (35, 41, 56, 67).
- **Problem:** PathSelection overrides no lifecycle methods; derived tools must hand-call
  init/dispose/reset/clear. Asymmetric with the base (which self-manages Handles/Snap).
- **Target:** have `NT_PathSelectionToolSystem` override the four lifecycle methods, call `base.*`,
  and run its own init/reset/clear/dispose; delete the now-redundant hand-calls in RoadShape/Parallel.
  Isolated and self-contained.

```csharp
protected override void OnCreate()       { base.OnCreate();        InitializeSelectionState(); }
protected override void OnDestroy()      { DisposeSelectionState(); base.OnDestroy(); }
protected override void OnStartRunning() { base.OnStartRunning();  ResetToNoSelection(); }
protected override void OnStopRunning()  { base.OnStopRunning();   ClearSelectionState(false); }
```

> **NT-001 (template-method `OnUpdate`) — dropped.** Investigated and reverted: only RoadShape and
> Parallel actually share the skeleton. Generate and the five node tools have genuinely different
> update loops (right-click handled before the handle short-circuit, cancel returning raw
> `inputDeps` without dispatch, no handle short-circuit at all, bespoke selection state), so the
> template would change their behavior. Not worth a base abstraction for two tools. See *Decisions*.

### NT-007 · eligibility query table · ✅ Done
- **✅ Done (2026-06-12):** built the `(flag, node query, edge query)` table in `OnCreate`; the four
  `Add…ByTargets` variants collapsed into `MarkEligibleByTargets` + a `MarkEligible` helper, with the
  `All` short-circuit preserved.
- **Where:** `Base/BaseToolSystem.cs` `AddEligibleNodesByTargets` (901), `AddEligibleEdgesByTargets`
  (931), and the two `…Filtered` variants (972, 1002).
- **Problem:** the same `TargetOption → query` mapping is duplicated across four methods; adding a
  target is a 4-place edit.
- **Target:** build a `(TargetOption flag, EntityQuery node, EntityQuery edge)` table once in
  `OnCreate` and iterate it; the only per-call variation is `EligibilityTarget` and fast vs
  `FilterAndAddEligible`. Keep the `All` fast-path short-circuit.

---

## Phase 2 — De-duplication

### NT-004 · one `NetCourse` emitter for the create-style tools
- **Where:** `OutputPreviewEdge` in `RoadShape/…Jobs.cs` (284), `Parallel/…Jobs.cs` (264),
  `Generate/…Jobs.cs` (94), `Connect/…Jobs.cs` (85); carrier `Utils/EdgeConfig.cs`.
- **Problem:** the `CreationDefinition` + two-`CoursePos` `NetCourse` *shape* is repeated ~7–8
  places. `EdgeConfig` is a leaky unifier: Connect reads `EC.Bezier.a/.d` for node positions while
  Generate reads `EC.StartNodePosition/.EndNodePosition`; scalar `EdgeConfig.Elevation` is dead.
- **Target:** first normalize `EdgeConfig` (one node-position convention; drop dead scalar
  `Elevation`), then introduce a Burst-static emitter and migrate the EdgeConfig tools (Generate,
  Connect, Parallel) to it. Leave RoadShape's edit-style apply (sets existing `Curve`s) on a
  separate small helper — its field values genuinely differ.

```csharp
internal static class NetCourseEmitter {
    public static void EmitPreview(ref EntityCommandBuffer ecb, in EdgeConfig e,
                                   CreationFlags flags, Entity original = default) { /* … */ }
}
```

### NT-013 · Circle generator via the ellipse path
- **Where:** `Generate/Generators/CircleGenerator.cs` (23–78) vs `OvalGenerator.cs` (27–81).
- **Problem:** near-verbatim duplicates; a circle is an ellipse with `radiusX == radiusZ`.
- **Target:** a private shared `GenerateEllipse(center, rx, rz, …)` both call (Circle with
  `rx = rz = CircleRadius + NetWidth*0.5f`), or have Circle delegate to Oval.

### NT-015 · de-duplicate snap guide-line emission
- **Where:** `Base/BaseToolSystem.Snap.cs` `NetIterator.HandleGuideLines`, start block (984–1064) vs
  end block (1066–1145); perp-right vs perp-left within each.
- **Problem:** ~80 lines copy-pasted 4× (start/end × along/perpendicular).
- **Target:** extract `EmitGuideLine(float3 origin, float2 dir, SnapLineFlags flags)` and call it for
  the four cases. Lower priority (ported from base game) but NT-owned now.

### NT-016 · eligibility recompute cost (needs an in-game check)
- **Where:** `PathSelection/…Selection.cs` `RecalculateEligibleNodes` (206–219, called from
  HandleAddNode 93 / HandleRemoveNode 150); BFS `…PathFinding.cs` `FindEligibleNodes` (26–71).
- **Problem:** every click strips `NT_Eligible` from all eligible nodes, runs a full network BFS,
  and re-adds — potentially thousands of archetype moves per click on a large connected network.
  Also allocates an undisposed `Allocator.Temp` array at 215.
- **Target:** pass `m_EligibleNodes.AsArray()` straight to `AddComponent` (drop the extra array);
  then diff the new reachable set vs the previous and add/remove only the delta, **or** recompute
  lazily on hover. Do after NT-007. Verify on a large, dense network that selection still feels
  responsive and eligibility is correct.

---

## Phase 3 — Decisions + cleanup

Low-risk polish. The intent questions that used to live here are now resolved — see *Decisions
(resolved)* below.

- **NT-003** — document the two sequenced meanings of `NT_Eligible`, or split into
  `NT_Eligible` (static target match) vs `NT_Reachable` (path-relative).
- **NT-006** — **doc-only.** Fix the misleading "Overrides base" comment on `GetAllowApply` (it
  `new`-hides a `protected virtual`; optionally rename to `UiAllowsApply` to kill the smell). **Do
  not change behavior** — dropping `ignoreErrors` is intended (anarchy is handled upstream by toggling
  the `ValidationSystem`, so `m_ErrorQuery` is already empty when anarchy is on; see decision 4).
- **NT-014** — derive the bezier-arc constant from `Segments` (`k = (4/3)·tan(π/(2·Segments))`)
  instead of the hardcoded 90°-only `Kappa`, or comment the `Segments==4` precondition.
- **Ease clamp (slope solver hardening)** — in `SlopeEaseInOutTransform.PreProcess` (or
  `BuildJobConfig`), clamp so `EaseInLength + EaseOutLength ≤ 1` before the Newton-Raphson solve in
  `SlopeUtils.FindParameterForPathRatio` (e.g. `easeOut = min(easeOut, 1 - easeIn)`). Each param's
  Max is already 0.5, so this only guards the exact-1.0 / future edge case.
- **NT-010** — drop Parallel's dead handle scaffolding (`RenderHandles = true` + empty
  `OnPathReady`/`OnPathExtended`/`OnPathTrimmed`/`OnSelectionCleared` stubs) until handles are real.
- **Cleanup backlog (Review NT-017…NT-029):** undisposed Temp arrays, dead code
  (`EdgeControlPointHeights`, commented guards), duplicated `using`s, `GetNetworkComposition`
  duplicated across two job structs, `CleanupHighlights`/`ClearAllHighlights` overlap, the
  reflection-grabbed Precise-Rotation action in the base, etc. Pick these up opportunistically while
  in the relevant file; none is a standalone task. See the review for the full list.

> **NT-008 — leave it alone (out of scope here).** `SlopeArch` and `CurveSmooth` are **coming soon**
> in a *future* effort. For this refactor: **keep all scaffolding untouched** (params, presets,
> dispatch cases, tooltips, the empty transform bodies, and the `Visible=false`/`Disabled=true` UI
> gating) and do **not** delete any of it during cleanup. Implementing the two transforms
> (`SlopeArch` → parabolic/arch; `CurveSmooth` → fill the empty `CurveSmoothTransform` bodies) is a
> separate future task, **not part of this effort.**

> **`config.ElevationLimit` — keep.** It is intentionally carried into the generate job for
> completeness even though no generator reads it yet. Do not flag or remove it as dead data.

---

## Decisions (resolved)

| # | Question | Resolution |
|---|---|---|
| 1 | NT-011 fix surface | **Handle-entity side.** Playtest: the parent-linked PositionHandle child stays synced; the AxisHandle does not. Fix `SyncParentPositionToChildHandles` to re-resolve + reposition the AxisHandle entity and its constraint; leave the working value-delta path. |
| 2 | SlopeArch / CurveSmooth fate (NT-008) | **Coming soon — keep, don't touch.** Keep all scaffolding and UI gating intact; **don't delete.** Implementing the transforms is a separate **future** effort, *not part of this refactor.* |
| 3 | Newton-Raphson convergence | **Add a gate.** Clamp `EaseInLength + EaseOutLength ≤ 1` before the solve (simple clamp). |
| 4 | `config.ElevationLimit` | **Keep for completeness.** Not dead data; don't remove. |
| 5 | Anarchy vs apply-gating (NT-006) | **Intended — keep behavior.** NT's simplified `GetAllowApply` is correct (anarchy disables the `ValidationSystem` upstream). NT-006 is doc-only. |
| 6 | NT-001 template-method `OnUpdate` | **Dropped (2026-06-12).** Implemented, then reverted. Only RoadShape + Parallel share the skeleton; Generate and the 5 node tools have genuinely different loops (right-click before the handle short-circuit, cancel returning raw `inputDeps`, no handle short-circuit, bespoke selection) that a base template would change. Not worth a base abstraction for two tools. Reconciling the other six is a behavior-changing effort — a separate future task if ever wanted. |

---

## Progress

- **Phase 0 — ✅ Done (2026-06-12).** NT-011 + NT-012 shipped together (behavior fix; verified via
  in-game repro, not unit-tested).
- **Phase 1 — ✅ Done (2026-06-12).** NT-002 + NT-007 landed, both behavior-preserving and built
  clean. NT-001 was implemented, then dropped and reverted (see *Decisions*).
- **Next — Phase 2 (De-duplication):** NT-004, NT-013, NT-015, NT-016. NT-016 needs an in-game check.

Build via the solution after each step and confirm no regression before starting the next.
