# NetworkTools — Tool-System Architecture Review

**Scope (priority order):** `NT_BaseToolSystem`, `NT_PathSelectionToolSystem`, `NT_RoadShapeToolSystem`, the Parallel tool (`NT_ParallelToolSystem`), the Generate tool (`NT_GenerateToolSystem`).
**Mode:** Read-only. No source files were modified; this report is the only output.
**Base-game reference:** `C:\Users\lucar\source\repos\GameDecomp1.3\Game` was available and consulted (`ToolBaseSystem.GetAllowApply` confirmed `protected virtual`, line 483; vanilla `GetAllowApply` consumers surveyed).
**Method:** The reviewer read `NT_BaseToolSystem` (4 partials, ~3,446 L) and `NT_PathSelectionToolSystem` (6 partials, ~759 L) in full first-hand, plus the cores of all three derived systems. A multi-agent pass deep-read each derived system's remaining partials (Jobs/JobMethods) and adversarially verified every High/Medium finding; verdicts are folded into Part B.

**Exact class names located (required by Scope):**
- **Parallel:** `NT_ParallelToolSystem` (namespace `NetworkTools.Systems.Tools.Parallel`) — extends `NT_PathSelectionToolSystem`. Partials: `ParallelToolSystem.{cs, Lifecycle.cs, Update.cs, JobMethods.cs, Jobs.cs, Tooltips.cs}` + `Core/{ParallelEnums, ParallelJobConfig}.cs`.
- **Generate:** `NT_GenerateToolSystem` (namespace `NetworkTools.Systems.Tools.Generate`) — extends `NT_BaseToolSystem` **directly**. Partials: `GenerateToolSystem.{cs, Lifecycle.cs, Update.cs, JobMethods.cs, Jobs.cs, Tooltips.cs}` + `Generators/{Grid, Circle, Oval}Generator.cs` + `Core/{GenerateMode, GenerateJobConfig, IGenerator}.cs`.

---

## Part A — Architecture Map and Executive Summary

### A.1 `NT_BaseToolSystem` contract

`NT_BaseToolSystem : ToolBaseSystem`, four partials:

| File | Lines | Responsibility |
|---|---:|---|
| `BaseToolSystem.cs` | 881 | Lifecycle, ~25 entity queries built in `OnCreate`, eligibility marking (`MarkEligibleEntities` + per-target if-ladders + `*Filtered` slow paths), highlight helpers, raycast helpers. `OnUpdate` is a **no-op**. |
| `BaseToolSystem.Parameters.cs` | 146 | Reflection-discovered `Parameters` (`ParameterSchema.Discover`), `WireParameterSubscribers` (3 `OnChanged` subscribers/param), JSON persistence. |
| `BaseToolSystem.Handles.cs` | 1174 | Declarative, spec-driven ECS handle system: build-from-spec, raycast, drag state machine, dispatch back to parameters. |
| `BaseToolSystem.Snap.cs` | 1245 | Self-contained Burst world-snap job ported from `NetToolSystem.SnapJob`; `TrySnapWorld` + virtual `GetSnapPrefab`/`GetSnapElevation`. |

**Lifecycle phases**

| Phase | Base responsibility | Derived expectation |
|---|---|---|
| `OnCreate` | Fetch ~10 managed systems; **reorder self to front of `m_ToolSystem.tools`**; bind apply/secondary/precise-rotation actions; alloc 2 `NativeReference`s; `InitializeHandles()`, `InitializeSnap()`; build ~25 queries. | `base.OnCreate()`, then set behavior flags + allocate tool state. **PathSelection-derived tools must also hand-call `InitializeSelectionState()`.** |
| `OnDestroy` | `DisposeHandles()`, `DisposeSnap()`, dispose refs, disable actions, re-enable 3 vanilla systems, `markersVisible=false`. | Dispose tool state, then `base.OnDestroy()`. **PathSelection tools must hand-call `DisposeSelectionState()`.** |
| `OnStartRunning` | Restore persisted snaps/targets/views/anarchy/params, reset hover refs, `UpdateActions()`. | `base`, then reset phase + selection. |
| `OnStopRunning` | `SaveParameters()`, disable actions, re-enable vanilla, `CleanupHandles()`, `CleanupHighlights()`. | `base`, then clear tool state. |
| `OnUpdate` | **No-op** (`return inputDeps`). | **Fully re-implemented by every derived tool.** |

**Overridable surface — there are _no abstract members_ on the base.** Behavior is configured two ways:
- **Public mutable fields toggled in derived `OnCreate`:** `RenderHandles`, `RenderTempEdges`, `RenderEligibleNodes/Edges`, `RenderSlope/Node/LengthTooltips`, `DisableVanillaValidation/NodeReduction/CourseSplit`, `UseCustomEligibilityFilter`, `EligibilityTarget`.
- **Virtual props/methods:** `AvailableSnaps/Targets/Views`, `SupportsAnarchy`, `ShouldRaycastHandles`, `GetActiveModeFlag()→0`, `GetSnapPrefab()→Null`, `GetSnapElevation()→0`, `FilterEligibleEntity()→true`, `OnEligibilityReset()`, `CleanupHighlights()`, `UpdateActions()`, `AddHighlight/RemoveHighlight/…`, `OnHandleDragStart/End/Click`, `InitializeRaycast()`, `GetRaycastResult(out ControlPoint)`. `GetAllowApply()` is declared **`public new`** (hides the base-game `protected virtual`; see NT-006).

**The declarative parameter/handle subsystem (newest, most polished):**
- `Parameters` is lazily reflection-discovered; first access wires **three** `OnChanged` subscribers per parameter: (1) `MarkUpdateNeeded`; (2) reverse-sync the parameter's handle entities (skips `ChangeOrigin.Handle`); (3) `Float3Parameter` parent→child delta propagation.
- Handles are declared **on the parameter** (`Parameter<T>.Handles : IHandleSpec<T>[]`). `RebuildHandlesForActiveMode()` filters by `GetActiveModeFlag()` and instantiates ECS handle entities; drag input flows back through `Parameter.SetValue(v, ChangeOrigin.Handle)`, closing the loop. This is a clean, self-consistent reactive data-binding layer.

### A.2 Inheritance map & contract usage

```
ToolBaseSystem (base game)
└── NT_BaseToolSystem                      params + handles + snap + eligibility + temp plumbing
    ├── NT_GenerateToolSystem              ← direct; richest, cleanest user of the modern contract
    └── NT_PathSelectionToolSystem         adds selection state machine + Dijkstra pathfinding
        ├── NT_RoadShapeToolSystem         adds cached PathData + IPathTransformation pipeline (Slope/Curve)
        └── NT_ParallelToolSystem          adds offset job; template hooks are empty stubs
   (out of scope: AddNode / RemoveNode / SlideNode / SuperNode / Connect)
```

| Contract member | Generate (direct) | RoadShape (path) | Parallel (path) |
|---|---|---|---|
| Declarative `Handles` on params | **Yes** — Position, GridDirectionPoint, CircleRadius, OvalAxisPoint, OvalRadiusZ (5) | Partial — 2 `AxisHandle`s (EaseIn/EaseOut) | **None** (sets `RenderHandles=true`, builds zero) |
| `GetActiveModeFlag` | `(int)Mode.Value` | `(int)Template.Value` | not overridden (→0) |
| World snap (`GetSnapPrefab/Elevation`) | **Yes** | no | no |
| `SupportsAnarchy` | yes | no (hard `DisableVanillaValidation`) | yes |
| Manual `Initialize/DisposeSelectionState` | n/a | **required, hand-called** | **required, hand-called** |
| `OnUpdate` skeleton | own copy | own copy | own copy (≈ verbatim of RoadShape) |
| Apply mechanism | `CreationDefinition`+`CreateDefinitionsJob` | **(see Part B — RoadShape's apply path)** | `CreationDefinition`+`CreateDefinitionsJob` |
| Phase ownership | `DerivePhase()` ← `m_ControlPoints.Length` | `UpdatePhaseFromSelection()` ← `m_SelectedNodes.Length` | inherited `UpdatePhaseFromSelection()` |
| Param→state sync | **hand-written `OnChanged` closures** (CP sync) | `Template.OnChanged` preset closure | none |

### A.3 Pattern inventory (and apparent era)

| Pattern | Where | Apparent era |
|---|---|---|
| Declarative params + observer (`OnChanged`) data-binding | `ParameterBase`/`Parameter<T>`, `WireParameterSubscribers` | **Later** — most polished, reflection-driven, documented |
| Spec-driven handle strategy (`IHandleSpec`, type-switch creation) | `BaseToolSystem.Handles.cs` | **Later** — co-designed with parameters |
| Strategy pipeline (`IPathTransformation` + `TransformPipeline`) | RoadShape transforms | **Later** — clean generic Burst strategy |
| Template-method state machine | `NT_PathSelectionToolSystem` (`OnPathReady`/`OnSelectionCleared`/…) | **Mid** — clean, abstract+virtual hooks |
| Burst job + `CreationDefinition`/`NetCourse` temp pipeline | every tool's `.Jobs.cs`/`.JobMethods.cs` | spans the whole project life |
| Imperative `switch` dispatch on a closed mode/phase set | `HandleTempEntities`/`HandleOutput` (phase), `CreateDefinitionsJob` (mode), eligibility if-ladders | mixed; eligibility ladders & per-tool `OnUpdate` read **earlier** |
| Hand-rolled imperative `OnChanged` closures | `GenerateToolSystem.Lifecycle` (4 CP-sync closures), `RoadShapeToolSystem` (`Template` preset) | transitional — imperative glue bridging the reactive system to a second state store |
| Manual behavior flags toggled in `OnCreate` | `RenderHandles`, `DisableVanilla*`, `EligibilityTarget`, … | earlier/simple |

*(Sections A.4–A.7 — overall characterization, top concerns, the cross-cutting verdict, the hypothesis-A verdict, and the maintainability rating — follow Part B so they can cite finding IDs. The map above is the Phase-1 deliverable.)*

---

## Part B — Detailed Findings

Every Medium/High finding below was adversarially verified against the source (a second reviewer tried to *refute* each one). Where verification changed the picture, the verdict is recorded inline: **two of the reviewer's initial findings were withdrawn (NT‑005, NT‑008 as originally framed)**, several were down‑rated, one factual error in Part A was corrected (NT‑003), and one **High‑severity bug (NT‑011)** was discovered during the sweep that the first pass missed.

### High

```
ID:         NT-011
Location:   Base/BaseToolSystem.Handles.cs — SyncParentPositionToChildHandles (469–485);
            related reverse-sync subscriber in BaseToolSystem.Parameters.cs (91–96);
            manifests at Generate/GenerateToolSystem.cs — OvalRadiusZ AxisHandle (78–90, Parent = nameof(Position))
Category:   Handle/Disposal Risk — parent-link propagation gap
Severity:   High
Confidence: Verified
Problem:    When a Float3Parameter parent moves, SyncParentPositionToChildHandles repositions only
            Circle and Rotation child handles (the `switch` at 474–478 returns null for every other
            spec type). The base per-parameter reverse-sync (Parameters.cs:91–96) only calls
            SyncToEntity on the *changed* parameter's own handles, not on children that reference it
            as Parent. An AxisHandle (or PositionHandle) parented to Position is therefore in neither
            path: after placement (Phase = Ready), dragging the Position handle moves the shape's
            origin but leaves the OvalRadiusZ axis handle anchored at the old center, so it visibly
            lags and then drags along a stale axis. This is the modern handle system's headline
            feature failing on the tool that exercises it most.
Rewrite:    Extend the child-follow switch to all parent-referencing spec types, and move the
            entity-center write into a single place keyed off NT_HandleParent rather than per-type:

            // BaseToolSystem.Handles.cs — SyncParentPositionToChildHandles
            foreach (var (entity, entry) in m_HandleEntries) {
                Float3Parameter resolvedParent = entry.Spec switch {
                    CircleHandle ch          => ch.ResolvedParent,
                    RotationHandle rh        => rh.ResolvedParent,
                    PositionHandle ph        => ph.ResolvedParent,   // NEW
                    AxisHandle ax            => ax.ResolvedParent,   // NEW
                    ComputedPositionHandle c => c.ResolvedParent,    // NEW
                    _                        => null
                };
                if (resolvedParent != parent || !EntityManager.Exists(entity)) continue;
                // For Axis/Position children, translate by the parent delta and refresh the
                // constraint origin (see NT-012); for Circle/Rotation keep the center-set.
                ...
            }

            Because Axis/Position children also carry their value in NT_HandlePosition *and* in the
            owning parameter, the parent delta must additionally be applied to the child parameter
            value (the existing m_ParentChildLinks delta mechanism in BaseToolSystem.Parameters.cs:
            102–115 already does this for Float3Parameter children — the gap is specifically the
            *handle-entity* center + constraint, not the value). Verify against both Oval (axis child)
            and any PositionHandle-with-Parent before propagating.
```

### Medium

```
ID:         NT-001
Location:   Base/BaseToolSystem.cs — OnUpdate (583–585, no-op); per-tool re-implementations in
            RoadShape/…Update.cs (OnUpdate 18–88, HandleTempEntities 95–105),
            Parallel/…Update.cs (OnUpdate 21–75, HandleTempEntities 82–92),
            Generate/…Update.cs (OnUpdate 14–46, HandleOutput 178–183),
            and 5 out-of-scope tools (AddNode/RemoveNode/SlideNode/SuperNode/Connect)
Category:   Architectural — missing template method / duplicated control flow
Severity:   Medium
Confidence: Verified
Problem:    The base OnUpdate is a pure pass-through, so all 8 tools re-implement the identical
            skeleton: `UpdateActions()` → `if (Ready && ProcessHandleInput) return dispatch` →
            raycast/selection → phase-`switch` dispatch. RoadShape's and Parallel's OnUpdate are
            near-verbatim. A change to the shared invariant (e.g. action gating, handle short-circuit
            order) is an 8-file parallel edit. **Caveat (verified):** the three `Apply()` methods
            differ legitimately — Parallel returns to `Phase = Ready` so the user can re-apply an
            offset (JobMethods.cs:89), while RoadShape/Generate are one-shot and `ResetToIdle()`. So
            the *Apply semantics* should NOT be force-unified; only the *update skeleton* should be
            hoisted.
Rewrite:    Make OnUpdate a base template with a small protected surface, leaving Apply per-tool:

            // NT_BaseToolSystem
            protected override JobHandle OnUpdate(JobHandle deps) {
                UpdateActions();
                if (RenderHandles && Phase == OperationPhase.Ready && ProcessHandleInput(deps))
                    return Dispatch(deps);
                HandleToolInput(deps);          // abstract: per-tool raycast + selection/CP mutation
                return Dispatch(deps);
            }
            protected abstract void      HandleToolInput(JobHandle deps);
            protected abstract JobHandle Dispatch(JobHandle deps);   // tool's phase switch (Update/Apply/Clear)

            Path-based tools can share a further intermediate that implements HandleToolInput in terms
            of HandlePathUpdate/HandleHover/HandleAddNode/HandleRemoveNode (today copy-pasted between
            RoadShape and Parallel verbatim).
```

```
ID:         NT-004
Location:   RoadShape/…Jobs.cs — OutputPreviewEdge (284) + ApplyCompositionToNetCourse (376);
            Parallel/…Jobs.cs — OutputPreviewEdge (264); Generate/…Jobs.cs — OutputPreviewEdge (94);
            Connect/…Jobs.cs — OutputPreviewEdge (85); also SuperNode/RemoveNode/SlideNode/AddNode .Jobs.cs;
            shared carrier Utils/EdgeConfig.cs
Category:   Duplication across systems
Severity:   Medium
Confidence: Verified (corrected from initial framing)
Problem:    The `CreationDefinition` + `NetCourse`-with-two-`CoursePos` builder is repeated across
            ~7–8 job structs. **Corrections from verification:** (a) the claim "only Generate uses
            EdgeConfig as the output carrier" is false — Connect also takes `EdgeConfig` into its
            OutputPreviewEdge and is Generate's closest twin; (b) the pattern is wider than the three
            in-scope tools; (c) the *field values* differ meaningfully (CreationFlags Recreate|Parent
            vs SubElevation, m_Original edge vs Null, elevation bezier-derived vs threshold vs
            EdgeConfig fields), so only the two-CoursePos *shape* is shared, not the contents.
            EdgeConfig is itself a leaky unifier: Connect reads `EC.Bezier.a/.d` for node positions
            while Generate reads `EC.StartNodePosition/.EndNodePosition` (EdgeConfig.cs:32,35), and
            the scalar `EdgeConfig.Elevation` (line 46) is dead in the output path (only the per-node
            elevations are consumed).
Rewrite:    Promote a single Burst-static emitter keyed on EdgeConfig, and migrate the existing
            EdgeConfig users (Generate, Connect, Parallel) to it first; leave the RoadShape/edit-style
            tools (which set existing Curves rather than create) on a separate, smaller helper:

            internal static class NetCourseEmitter {
                public static void EmitPreview(ref EntityCommandBuffer ecb, in EdgeConfig e,
                                               CreationFlags flags, Entity original = default) { … }
            }

            Before extracting, normalize EdgeConfig so every consumer reads the same fields for node
            position (drop the Bezier.a/.d fallback in Connect) and delete the dead scalar Elevation.
```

```
ID:         NT-007
Location:   Base/BaseToolSystem.cs — AddEligibleNodesByTargets (901), AddEligibleEdgesByTargets (931),
            AddEligibleNodesByTargetsFiltered (972), AddEligibleEdgesByTargetsFiltered (1002)
Category:   Duplication — repeated flag→query mapping table
Severity:   Medium
Confidence: Verified
Problem:    Four methods each contain the same TargetOption → cached-query mapping (All early-return
            + 5 non-exclusive flag checks for Road/Path/Rail/Waterway/InvisiblePath). Adding a target
            flag is a 4-place edit. The node/edge split and the fast/filtered split are real, but the
            mapping itself need not be repeated.
Rewrite:    Build a `(TargetOption flag, EntityQuery node, EntityQuery edge)` table once in OnCreate
            and iterate it; the only per-call variation is `EligibilityTarget` and whether to call
            `AddComponent(query)` (fast) or `FilterAndAddEligible(query)` (filtered):

            foreach (var (flag, nodeQ, edgeQ) in m_TargetQueryTable) {
                if ((targets & flag) == 0) continue;
                var q = EligibilityTarget == EligibilityTarget.Edge ? edgeQ : nodeQ;
                if (UseCustomEligibilityFilter) FilterAndAddEligible(q); else EntityManager.AddComponent<NT_Eligible>(q);
            }
            (Keep the `All` fast path as a special-case short-circuit.)
```

```
ID:         NT-012
Location:   Handles/AxisHandle.cs — SyncToEntity (46–50); constraint creation in
            Base/BaseToolSystem.Handles.cs — CreateHandleFromSpec (211–217) and
            ResolvePositionConstraintFields (338–366)
Category:   Lifecycle / contract gap (sibling of NT-011)
Severity:   Medium
Confidence: Verified
Problem:    AxisHandle.SyncToEntity writes only NT_HandlePosition; the NT_HandleConstraints (axis
            direction, origin, Min/Max distance bounds derived from path length) are computed once at
            creation and never refreshed. When a handle's dynamic endpoints move without a full
            RebuildHandlesForActiveMode, dragging stays constrained to the stale axis/bounds. Latent
            today only because tools happen to rebuild after endpoint changes — but the SyncToEntity
            contract silently omits constraint refresh, which is exactly the seam NT-011 falls through.
Rewrite:    Have SyncToEntity (for Axis/constrained Position handles) recompute and SetComponentData
            the NT_HandleConstraints from the spec's endpoint delegates alongside NT_HandlePosition,
            so a parent move or value change keeps the constraint coherent without a full rebuild.
```

```
ID:         NT-013
Location:   Generate/Generators/CircleGenerator.cs — Generate (23–78);
            Generate/Generators/OvalGenerator.cs — Generate (27–81)
Category:   Duplication — Method bodies near-identical
Severity:   Medium
Confidence: Verified
Problem:    A circle is an ellipse with radiusX == radiusZ; the two Generate bodies are identical
            except for the radius/tangent expressions (setup block, node pre-compute loop, per-segment
            bezier loop, and EdgeConfig population are copy-pasted). Any change to elevation/flags/
            parity handling must be made twice and can drift.
Rewrite:    Express Circle via the ellipse path — a private shared `GenerateEllipse(center, rx, rz, …)`
            that both call (Circle with rx = rz = CircleRadius + NetWidth*0.5f), or make
            CircleGenerator delegate to OvalGenerator with equal radii.
```

```
ID:         NT-014
Location:   Generate/Generators/CircleGenerator.cs (Kappa 14–15, used 37, 59–60);
            Generate/Generators/OvalGenerator.cs (Kappa 14–15, used 40, 62–63)
Category:   Magic constant with an undocumented invariant
Severity:   Medium
Confidence: Verified
Problem:    `Kappa = 0.5522847498f` is the cubic-bezier circle-approximation constant valid only for a
            90° arc, i.e. only when `Segments == 4`. It is applied as the control-handle length for an
            arc of `2π/Segments`. Because Kappa is a `const` decoupled from Segments, changing Segments
            to anything but 4 silently yields non-circular curves. The Segments/Kappa coupling is
            unstated.
Rewrite:    Derive the handle length from Segments — `k = (4f/3f) * tan(π / (2*Segments))` — so the
            arc approximation stays correct for any Segments, and drop the hardcoded constant (or at
            minimum comment the `Segments==4` precondition on Kappa).
```

```
ID:         NT-015
Location:   Base/BaseToolSystem.Snap.cs — NetIterator.HandleGuideLines, start-node block (984–1064)
            vs end-node block (1066–1145); within each, perp-right (1013–1037 / 1094–1118) vs
            perp-left (1039–1062 / 1120–1143)
Category:   Duplication inside the ported snap job
Severity:   Medium
Confidence: Verified
Problem:    The start-node and end-node guide-line emitters are ~80 lines of near-identical code
            differing only in startPos/startDir vs endPos/endDir; inside each, the perpendicular-right
            and perpendicular-left sub-blocks differ only by MathUtils.Right vs MathUtils.Left. The
            same SnapLine / CalculateSnapPriority / AddSnapPosition sequence is copy-pasted four times,
            so any guide-line tuning is a four-place edit. (This is ported from the base game, so it is
            lower priority than NT-authored duplication, but it lives in NT-owned code now.)
Rewrite:    Extract `EmitGuideLine(float3 origin, float2 dir, SnapLineFlags flags)` and call it for
            {start-along, end-along} and, for dead-end nodes, {±perp at start, ±perp at end}.
```

```
ID:         NT-016
Location:   PathSelection/PathSelectionToolSystem.Selection.cs — RecalculateEligibleNodes (206–219),
            called from HandleAddNode (93) and HandleRemoveNode (150); BFS in
            PathSelectionToolSystem.PathFinding.cs — FindEligibleNodes (26–71)
Category:   Performance — structural-change churn per interaction
Severity:   Medium
Confidence: Suspected (scales with map size)
Problem:    Every node add and every trim does RemoveComponent<NT_Eligible> over all currently-eligible
            nodes, a full BFS over the entire connected network, then AddComponent<NT_Eligible> over
            all reachable nodes — i.e. potentially thousands of archetype moves per click on a large,
            highly-connected network. Functionally correct, but a real per-interaction cost; the
            temp array at 215 is also allocated `Allocator.Temp` and never disposed (frame-scoped, but
            wasteful), and `AddComponent` could take `m_EligibleNodes.AsArray()` directly.
Rewrite:    (1) Pass `m_EligibleNodes.AsArray()` straight to AddComponent (drop the extra NativeArray).
            (2) Diff the new reachable set against the previous one and only add/remove the delta, or
            recompute eligibility lazily (on hover) rather than eagerly on every add/trim.
```

```
ID:         NT-006
Location:   Base/BaseToolSystem.cs — GetAllowApply (1044 XML doc, 1047 body);
            base game ToolBaseSystem.GetAllowApply (decomp line 483, protected virtual)
Category:   Misleading contract / behavioral simplification
Severity:   Low–Medium
Confidence: Verified
Problem:    `public new bool GetAllowApply()` was flagged as "breaking override semantics." Verification
            shows the override *breakage is not realized*: ToolBaseSystem never calls GetAllowApply
            internally, and NT_BaseToolSystem extends ToolBaseSystem directly (not NetToolSystem), so
            no base-game virtual dispatch reaches an NT instance; the sole consumer is UISystem via an
            `NT_BaseToolSystem`-typed reference, where `new` resolves correctly. The *real* issues are
            (a) the XML doc at 1044 says "Overrides base" when it hides, and (b) the NT body drops the
            base game's `m_ToolSystem.ignoreErrors` short-circuit and `!originalDeleted` guard — so for
            anarchy-supporting tools (Generate, Parallel: `SupportsAnarchy => true`), the apply button
            stays disabled while validation errors exist even when the player has error-ignoring on.
            Whether that is intended is an open question (see Part C.5).
Rewrite:    Fix the doc comment ("hides, intentionally widening visibility for the UI"). If anarchy
            should relax apply-gating, fold `(AnarchyEnabled || m_ErrorQuery.IsEmptyIgnoreFilter)` into
            the body. Consider renaming to `UiAllowsApply()` to remove the `new`-on-virtual smell.
```

### Low

```
ID:         NT-002
Location:   PathSelection/PathSelectionToolSystem.Lifecycle.cs — InitializeSelectionState (18),
            DisposeSelectionState (31); hand-called from RoadShape/…Lifecycle.cs (71, 81, 103, 110)
            and Parallel/…Lifecycle.cs (35, 41, 56, 67)
Category:   Lifecycle Responsibility Bleed — hand-call contract
Severity:   Low (no live defect; clean refactor target)
Confidence: Verified
Problem:    NT_PathSelectionToolSystem overrides none of the four lifecycle methods; its NativeLists
            are allocated/freed by InitializeSelectionState/DisposeSelectionState that derived tools
            must call by hand, and the same hand-wiring extends to ResetToNoSelection (OnStartRunning)
            and ClearSelectionState (OnStopRunning). This is asymmetric with the base, which
            self-manages Handles/Snap inside its own OnCreate/OnDestroy. Both existing subclasses get
            it right, so there is no current bug — but a future PathSelection tool that forgets the
            Initialize call gets uninitialized lists, and the contract is invisible.
Rewrite:    Have NT_PathSelectionToolSystem override OnCreate/OnDestroy/OnStartRunning/OnStopRunning,
            call base, and run its own init/reset/clear/dispose — mirroring the base's self-management.
            Derived tools then override only for tool-specific extras (and can drop their hand-calls).
```

```
ID:         NT-003
Location:   Base/BaseToolSystem.cs — MarkEligibleEntities (827) / AddEligibleNodesByTargets (901);
            PathSelection/…Selection.cs — RecalculateEligibleNodes (206), ResetToNoSelection (277→288),
            OnEligibilityReset (310); Base/BaseToolSystem.cs — RefreshEligibility (843)
Category:   Overloaded component / contract spread across methods
Severity:   Low (sequenced, not simultaneously conflicting)
Confidence: Verified (with a Part A correction)
Problem:    NT_Eligible carries two meanings: "every node matching the target flags" (base
            MarkEligibleEntities) and "reachable from the current path endpoint" (PathSelection's
            RecalculateEligibleNodes replaces it after the first selection). **Correction to Part A:**
            the "any matching node" consumers are AddNode/RemoveNode/SlideNode/SuperNode and the idle
            phase of the path tools — *not Generate*, which sets `AvailableTargets => None` and never
            touches NT_Eligible. The two meanings are sequenced (narrow on select, re-broaden on clear
            via OnEligibilityReset→ResetToNoSelection), so no code path reads one meaning while the
            other is in force — hence Low, a clarity hazard rather than a bug. The eligibility lifecycle
            spans three methods (RefreshEligibility, OnEligibilityReset, ResetToNoSelection) with a
            subtle "strip-then-don't-remark" ordering contract (resetEligibleNodes:false at 313).
Rewrite:    Document the two-phase meaning on NT_Eligible, or split into NT_Eligible (static target
            match) vs NT_Reachable (path-relative), so the path search and the raycast gate read
            unambiguous components.
```

```
ID:         NT-009
Location:   Base/BaseToolSystem.cs — m_LastHoveredEntity (141);
            Base/BaseToolSystem.Handles.cs — ProcessHandleIdleState (1178–1211, stores handle entity);
            RoadShape/…Update.cs (63, 69) and Parallel/…Update.cs (53, 58, store node entity)
Category:   Overloaded field — clarity smell
Severity:   Low (benign at runtime)
Confidence: Verified
Problem:    One NativeReference holds "last hovered handle" (handle idle state) and "last hovered node"
            (per-tool update). Verification shows it is self-correcting: the field is reset to Null in
            the same ProcessHandleIdleState call on the frame the handle is left (Handles.cs:1193), and
            a node entity can never equal a handle entity, so a false "same-entity" (missed) transition
            is impossible; the only residual is one redundant hover refresh via the defensive
            PendingState branch (1222–1226). **Correction:** the comparison/write live in the per-tool
            OnUpdate partials, not in PathSelectionToolSystem.Hover.cs (which only reads the field).
Rewrite:    Give handle hover its own field (m_LastHoveredHandle) distinct from the node hover field,
            so the two state machines don't share storage.
```

```
ID:         NT-010
Location:   Parallel/ParallelToolSystem.cs — OnPathReady/OnSelectionCleared/OnPathExtended/OnPathTrimmed
            (39–64); Parallel/ParallelToolSystem.Lifecycle.cs — OnCreate (30, RenderHandles = true)
Category:   Dead scaffolding / misleading stubs
Severity:   Low
Confidence: Verified
Problem:    Parallel sets RenderHandles = true but never builds a handle (no RebuildHandlesForActiveMode
            / CreateXHandle anywhere in its 6 partials), so m_Handles stays empty: the
            `Phase==Ready && ProcessHandleInput` branch (Update.cs:28) is dead (ShouldRaycastHandles is
            false), and DestroyAllHandles in OnSelectionCleared/ResetToIdle is a no-op. The template
            hooks are debug-log-only stubs with "In a full implementation" comments. **Note:** only the
            *handle* side is dead — preview and apply are fully functional via the OnUpdate phase
            machine; the tool's real on-screen affordance is `requireNetArrows = true` (Lifecycle.cs:52),
            not handles. OnPathExtended/OnPathTrimmed are base no-ops, so overriding them with only a
            debug log adds nothing.
Rewrite:    Drop `RenderHandles = true` and the empty overrides until handles are actually implemented,
            or implement offset handles. Removes a misleading signal that the tool drives handles.
```

```
ID:         NT-008  (reframed — original "silent failure" claim WITHDRAWN)
Location:   RoadShape/Core/ShapeTransformTemplate.cs — SlopeArch (15–16, Visible=false),
            CurveSmooth (21–22, Disabled=true); RoadShape/…Jobs.cs Execute switch (71–73 empty SlopeArch,
            78–81 dispatches CurveSmooth); RoadShape/Transforms/CurveSmoothTransform.cs (all bodies empty);
            RoadShape/…Lifecycle.cs ApplyTemplatePreset (123–146)
Category:   Incomplete-feature scaffolding wired into the pipeline
Severity:   Low
Confidence: Verified
Problem:    Original finding claimed SlopeArch is "selectable and silently does nothing." **Refuted:**
            SlopeArch is `Visible=false` and CurveSmooth is `Disabled=true`, and the UI tab bar filters
            both out, so neither is user-reachable. The real, smaller issue is half-wired placeholders:
            SlopeArch has params (ArchHeight/ArchPosition), a preset branch, and tooltips but an empty
            transform case; CurveSmooth is *dispatched* (Jobs.cs:78) to a CurveSmoothTransform whose
            PreProcess/Process/PostProcess are all empty TODOs. The scaffolding-vs-implementation
            mismatch is in-progress debt, not a runtime defect.
Rewrite:    Gate the dispatch on implemented templates (or guard the empty cases with an explicit
            "not implemented" no-op + log), and keep the param/preset/tooltip scaffolding behind the
            same Visible/Disabled flag as the enum so UI and job agree on what exists.
```

```
ID:         NT-005  (WITHDRAWN after verification — was: "Generate's OnChanged closures duplicate the base reverse-sync")
Location:   Generate/GenerateToolSystem.Lifecycle.cs — OnCreate closures (37–65);
            Base/BaseToolSystem.Parameters.cs — WireParameterSubscribers (91–96)
Category:   (n/a — refuted)
Severity:   Not an issue
Confidence: Verified
Problem:    The hypothesis was that Generate's 4–5 hand-written OnChanged closures re-implement the
            base's declarative reverse-sync. **Refuted:** the two mechanisms run on mutually exclusive
            triggers and write disjoint targets. Base reverse-sync fires only on *non-Handle* origin
            and writes the ECS handle entity (NT_HandlePosition) via SyncToEntity; the Generate closures
            fire only on `ChangeOrigin.Handle` and write the tool's *domain* state (m_ControlPoints) +
            re-derive geometry via the generators — something the IHandleSpec surface cannot express.
            There is no duplication; this is the legitimate "imperative glue to a second state store"
            already noted in A.3.
Residual:   One genuine (Low) smell remains nearby: InitializeFromSecondPoint (Update.cs:122–123)
            unconditionally writes BOTH GridDirectionPoint.Value and OvalAxisPoint.Value regardless of
            Mode, causing redundant cross-mode parameter writes (harmless — the wrong-mode parameter
            has no active handle, so reverse-sync no-ops). Guard each write by Mode.
```

**Low cluster** (readability/clarity, no behavioral risk — grouped to avoid padding):

| ID | Location | Issue |
|---|---|---|
| NT-017 | PathSelection/…Selection.cs — HandleRemoveNode, EndNodeSelected branch (155–161) | Trimming a 2→1 node path calls `OnSelectionCleared()` while state is now StartNodeSelected (start node still selected), contradicting that hook's documented "back to NoSelection" contract (PathSelectionToolSystem.cs:58–59). Benign today; a trap for future overrides. Use a distinct `OnPathIncomplete` signal. (Verified) |
| NT-018 | PathSelection/…State.cs — GetSelectedNodes (101), GetPathNodes (109), GetPathEdges (117) | `m_X.ToArray(Allocator.Temp).ToArray()` allocates an intermediate Temp NativeArray that is never disposed before the managed copy. Use `m_X.AsArray().ToArray()`. (Verified) |
| NT-019 | RoadShape/Core/TransformPipeline.cs — ComputeNodePositions (55, 67, 78) | The loop already `continue`s for endpoints, so for interior nodes `i>0` and `i<edges.Length` are always true and `contributors` is always 2 — dead guards that obscure the "interior node = average of two edge deltas" invariant. (Verified) |
| NT-020 | RoadShape/Utils/EdgeControlPointHeights.cs (whole file) | Struct defined but never referenced anywhere; transforms pass the four heights as positional floats instead. Dead code. (Verified) |
| NT-021 | RoadShape/…Jobs.cs GetNetworkComposition (107) **and** RoadShape/…PathData.cs GetNetworkComposition (261) | Identical method duplicated across two job structs. Acceptable for Burst (no shared instance methods across structs) but extract a Burst-static `NetCompositionUtils.From(Upgraded)`. (Verified) |
| NT-022 | Base/BaseToolSystem.cs — CleanupHighlights (661) vs ClearAllHighlights (815) | CleanupHighlights = `RemoveComponent<NT_Eligible>` + the exact body of ClearAllHighlights; have it call ClearAllHighlights. (Verified) |
| NT-023 | Base/BaseToolSystem.cs — OnCreate (384–387) | The "Precise Rotation" action is grabbed by reflection on InputManager's private `toolActionCollection` for *every* tool, though only Generate uses precise rotation. Fragile + misplaced; move to Generate or behind a virtual opt-in. (Suspected — depends on base-game internals) |
| NT-024 | Handles/ComputedPositionHandle.cs — SyncToEntity (20) | Calls `ComputePosition(...)` with no null guard (unlike PositionHandle.SyncToEntity). Latent NRE if ever declared without the delegate; the type is currently uninstantiated. (Suspected) |
| NT-025 | Base/BaseToolSystem.Snap.cs — TrySnapWorld (164–168) + Execute fallback (278–280) | `won` is derived from the fallback's leftover `m_SnapPriority` rather than from whether a candidate replaced the raw hit; benign only because callers pass a default-priority raycast hit. Track replacement explicitly or zero the fallback priority. (Suspected) |
| NT-026 | Generate/Core/IGenerator.cs (5–9); Generators' static `Initialize` (Grid:14, Circle:17, Oval:17); dispatch Generate/…Update.cs InitializeFromSecondPoint (116–120) | IGenerator declares only `Generate`; the required static `Initialize(tool, float3)` is convention-only (can't be on the pre-static-abstract interface), enforced solely at the switch. Document the convention on IGenerator. (Verified) |
| NT-027 | RoadShape/Transforms/SlopeEaseInOutTransform.cs — PreProcess (30–33) | Start control points use `ctx.StartHeight`; end control points use `ctx.EndPosition.y` directly (same value, asymmetric access). Add a matching `EndHeight` accessor and use it. (Verified) |
| NT-028 | RoadShape/…JobMethods.cs (6–7, 13–14) | Duplicate `using Game.Prefabs;` and `using NetworkTools.Components;`. (Verified) |
| NT-029 | RoadShape/…Jobs.cs — PreviewConnectedEdges (212–214) | Commented-out `HasNodePositionChanged` guard left in place — dead commented code; remove or restore intentionally. (Verified) |

---

## Part A (continued) — Executive Summary

### A.4 Overall characterization

The tool layer shows a clear **maturation gradient**. The newest subsystems — declarative parameters with observer-style data-binding, the spec-driven handle system, the `IPathTransformation` strategy pipeline, and the ported world-snap job — are genuinely good: cohesive, documented, Burst-aware. The older substrate (the per-tool `OnUpdate` skeleton, the target→eligibility if-ladders, the manual `Initialize*State` lifecycle calls, the repeated `NetCourse` builders) is where debt sits. After adversarial verification, the debt is **mostly mechanical duplication plus a few fragile contracts — not a structure that resists extension** — with **one real correctness bug (NT-011)** in the newest subsystem.

A notable outcome of the verification pass: the codebase is **more internally consistent than the initial read suggested**. The two "inconsistency" hypotheses that looked strongest on a first pass (event-vs-switch mixing → NT-005, and a silently-broken mode → NT-008) were both refuted on close reading. What remains is duplication and contract-clarity debt, which is cheaper to retire than a paradigm clash would have been.

### A.5 Top concerns (ranked by impact)

1. **NT-011 (High, correctness):** the handle system's parent-follow propagation skips Axis/Position children, so Generate's oval radius handle lags when the origin is dragged. The one item that must be fixed regardless of any refactor decision. NT-012 is its latent sibling (constraints never refreshed).
2. **NT-001 (Medium, structure):** no base `OnUpdate` template → the same update skeleton is duplicated across 8 tools; the cleanest single lever for reducing drift.
3. **NT-004 / NT-013 / NT-015 (Medium, duplication):** the `NetCourse` emitter (~7–8 sites), the Circle/Oval generators (near-verbatim), and the snap guide-line blocks (4×) are the largest copy-paste clusters.
4. **NT-016 (Medium, performance):** full eligibility strip + network BFS + re-mark on every click.
5. **NT-007 / NT-002 / NT-003 (contract clarity):** the eligibility mapping is a 4-place edit; PathSelection's lifecycle is a hand-call contract; `NT_Eligible` carries two sequenced meanings.

### A.6 Cross-cutting verdict — is `NT_BaseToolSystem` over-fitted to one family?

**No — and the evidence runs opposite to the author's suspicion.** The base's richest, newest investments (declarative params, spec-driven handles, world snap) are exercised *most* by the **generative** tool, not the path family: Generate uses five declarative handles, `GetActiveModeFlag`, and world snap cleanly; RoadShape uses the handle system for exactly two controls; **Parallel uses it for none** (NT-010). The path family leans on the *older* base plumbing (eligibility/highlight/temp) and bolts on its own hierarchy (selection state machine + Dijkstra + cached path data).

Two qualifications, both now evidenced:
- The base is genuinely general, but its handle parent-follow is **under-tested precisely where the generative tool relies on it** — NT-011/NT-012 are bugs in the path that Generate, not the path tools, drives hardest. So "well-fit to Generate" is true *in intent* but has a real gap *in implementation*.
- **Correction to the initial read:** Generate does **not** use the base eligibility contract at all (`AvailableTargets => None`, no `NT_Eligible` references). The "any matching node" eligibility consumers are AddNode/RemoveNode/SlideNode/SuperNode and the idle phase of the path tools (NT-003). The base is not bent toward path tools; rather, PathSelection re-purposes one base contract (eligibility) and declines to integrate with another (lifecycle).

### A.7 Hypothesis-A verdict (delegate/event vs switch/direct-call) — **largely refuted**

The split is **principled, not accidental**: `OnChanged` events are used for *data-binding* (parameter ⇆ handle ⇆ UI), a genuinely reactive concern; `switch`/direct dispatch is used for *mode→algorithm selection* (`Config.Template`, `Mode`, phase routing), a closed set that also *must* be a switch inside Burst jobs. The events do not leak across sessions (publisher and subscriber share the system lifetime; wiring happens once). The single place that looked like a same-concern collision — Generate's hand-written `OnChanged` closures (NT-005) — was **refuted**: those closures bridge to a second state store (`m_ControlPoints`) and re-run generators, which the declarative handle surface cannot express, and they run on the opposite trigger from the base reverse-sync. There is **no systemic event-vs-switch inconsistency**. The real, smaller residue is duplication (Part B) and one redundant cross-mode write (NT-005 residual).

### A.8 Maintainability rating — **Fair** (incremental refactor advisable)

The core abstractions are sound and worth preserving; Generate proves the base extends cleanly, and the verification pass found the codebase *more* consistent than first appeared. The debt is concentrated in (a) the absent `OnUpdate`/output templates causing duplication, (b) a hand-call lifecycle contract in PathSelection, and (c) a few overloaded/under-documented contracts. None forces incorrect extension; each is retireable by targeted extraction. That is squarely **Fair**, not Poor. The one caveat is **NT-011**, a correctness bug that is independent of the refactor and should be fixed first.

---

## Part C — Refactor Recommendation

### C.1 Verdict: **incremental refactor** (not a structural rewrite, not fixes-only)

A structural rewrite is unjustified — the abstractions that would survive it (declarative params, handle specs, transform pipeline, snap port, phase/temp pipeline, the PathSelection state machine) are exactly the parts that are already good. Fixes-only is insufficient because the top concerns (NT-001, NT-002, NT-004, NT-007) are *shapes*, not point defects: leaving them in place guarantees continued drift as tools are added. The right scope is a sequence of behavior-preserving extractions on a stable base, preceded by the NT-011 correctness fix.

### C.2 What to preserve (do not discard in the refactor)

- **The declarative parameter system** (`ParameterBase`/`Parameter<T>`, reflection discovery, `OnChanged`, JSON persistence). It is the spine of both the UI codegen and the handle binding, and it is clean.
- **The spec-driven handle system** — *repair it (NT-011/012), don't replace it.* The `IHandleSpec` + `RebuildHandlesForActiveMode` + drag→`SetValue(…, Handle)` loop is a strong design; the parent-follow gap is a bug in one method, not a flaw in the model.
- **The `IPathTransformation` + `TransformPipeline.Execute<T>` strategy** — a textbook Burst-friendly strategy; the smooth/arch gaps are unfinished features, not design faults.
- **The ported `SnapPlacementJob`** — faithfully mirrors `NetToolSystem.SnapJob`; keep it (just de-duplicate the guide-line blocks, NT-015).
- **`OperationPhase` + the temp-entity (`CreationDefinition`/`NetCourse`) pipeline** and the **per-tool `Apply()` semantics** (one-shot vs re-applyable are intentionally different — NT-001 caveat).
- **The PathSelection state machine and Dijkstra path search** — coherent and well-documented; only its lifecycle wiring (NT-002) and eligibility recompute cost (NT-016) need work.
- **`EdgeConfig` as the cross-tool edge carrier** — keep the concept, but tighten it (one node-position convention, drop dead scalar `Elevation`) so it can actually unify output (NT-004).

### C.3 Target shape

A **slimmer base + family intermediate classes**, with composition for the lifecycle and dispatch concerns that are currently copy-pasted:

```
NT_BaseToolSystem            // owns: params, handles (fixed), snap, eligibility(table), temp pipeline,
  │                          //       + a template-method OnUpdate (NT-001)
  ├── NT_GenerateToolSystem  // control-point placement; richest handle/snap user
  └── NT_PathSelectionToolSystem   // self-managing lifecycle (NT-002); owns selection + pathfinding
        ├── NT_RoadShapeToolSystem
        └── NT_ParallelToolSystem
```

```csharp
public abstract partial class NT_BaseToolSystem : ToolBaseSystem {
    // NT-001: the skeleton lives here; tools fill the two hooks.
    protected override JobHandle OnUpdate(JobHandle deps) {
        UpdateActions();
        if (RenderHandles && Phase == OperationPhase.Ready && ProcessHandleInput(deps))
            return Dispatch(deps);
        HandleToolInput(deps);                 // raycast + selection / control-point mutation
        return Dispatch(deps);                 // tool's Update / Apply / Clear phase switch
    }
    protected abstract void      HandleToolInput(JobHandle deps);
    protected abstract JobHandle Dispatch(JobHandle deps);

    // NT-007: data-driven eligibility instead of four if-ladders.
    private readonly List<(TargetOption flag, EntityQuery node, EntityQuery edge)> m_TargetQueryTable = new();
}

public abstract partial class NT_PathSelectionToolSystem : NT_BaseToolSystem {
    // NT-002: self-manage — derived tools stop hand-calling Initialize/Dispose/Reset/Clear.
    protected override void OnCreate()       { base.OnCreate();        InitializeSelectionState(); }
    protected override void OnDestroy()      { DisposeSelectionState(); base.OnDestroy(); }
    protected override void OnStartRunning() { base.OnStartRunning();  ResetToNoSelection(); }
    protected override void OnStopRunning()  { base.OnStopRunning();   ClearSelectionState(false); }

    // shared raycast+selection skeleton both RoadShape and Parallel copy verbatim today:
    protected sealed override void HandleToolInput(JobHandle deps) { /* hover / add / remove */ }
}

// NT-004: one emitter for the create-style tools (Generate, Connect, Parallel).
internal static class NetCourseEmitter {
    public static void EmitPreview(ref EntityCommandBuffer ecb, in EdgeConfig e,
                                   CreationFlags flags, Entity original = default) { /* … */ }
}
```

### C.4 Sequencing (risk-ordered; prerequisites noted)

1. **Correctness first — NT-011**, then **NT-012** (its enabling gap). Independent of everything else; ship and verify before refactoring so the refactor isn't blamed for a pre-existing bug. *Prerequisite for trusting any handle-system change.*
2. **Base-contract changes (no behavior change):**
   - **NT-002** PathSelection self-managing lifecycle — small, isolated, unblocks deleting hand-calls in RoadShape/Parallel.
   - **NT-001** template-method `OnUpdate` + shared path-tool `HandleToolInput`. *Do after NT-002* (the path intermediate is the natural home for the shared input skeleton). Keep each tool's `Apply()` distinct.
   - **NT-007** eligibility query table.
3. **Mechanical propagation / de-duplication:**
   - **NT-004** unify `NetCourse` emission for the EdgeConfig tools (normalize EdgeConfig first), then **NT-013** (Circle via ellipse) and **NT-015** (guide-line helper).
   - **NT-016** eligibility recompute (delta or lazy) — *after* NT-007 so the eligibility surface is already consolidated.
4. **Clarity + cleanup:** NT-003 (document or split `NT_Eligible`), NT-006 (doc + anarchy decision), NT-009/NT-010, and the NT-017…NT-029 Low cluster.

NT-008 and NT-005-residual are "decide, then delete-or-finish" items, not blockers.

### C.5 Open questions (resolve before/at the relevant step)

- **Anarchy vs apply-gating (NT-006):** is it intended that anarchy-enabled tools cannot apply while validation errors exist? The base game's `GetAllowApply` honors `ignoreErrors`; the NT override drops it. Decide before "fixing" — it may be deliberate.
- **NT-011 fix surface:** does the oval-center drag need the fix in the *handle-entity* path (`SyncParentPositionToChildHandles`), the *parameter-value* path (`m_ParentChildLinks` delta), or both? Needs an in-game drag test of OvalRadiusZ while moving Position.
- **`config.ElevationLimit` (NT-026 area):** it is resolved into every generate job but never read; was per-prefab elevation clamping intended, or is it dead data?
- **Newton-Raphson convergence (sweep):** `SlopeUtils.FindParameterForPathRatio` runs a fixed 8 iterations with no fallback; confirm `EaseInLength + EaseOutLength ≤ 1` is always enforced, or add a guard.
- **SlopeArch / CurveSmooth (NT-008):** are these shipping soon (finish the transforms) or abandoned (delete the params/presets/dispatch)? The half-wired state should not persist.

### C.6 Verification provenance

Findings were produced from a full first-hand read of `NT_BaseToolSystem` (4 partials) and `NT_PathSelectionToolSystem` (6 partials) plus the cores of all three derived systems, then a multi-agent pass adversarially verified every Medium/High finding and deep-read the remaining partials (RoadShape transforms, the snap-iterator tail, handle specs, Circle/Oval generators, PathSelection internals). That pass withdrew NT-005 and the original NT-008, down-rated NT-001/002/003/004/006/009, corrected the Generate/eligibility error in A.6, and surfaced NT-011 (High), NT-012/013/014/015/016 (Medium), and the NT-017…NT-029 cluster. Suspected-confidence items (NT-016, NT-023, NT-024, NT-025, and the elevation/convergence open questions) depend on runtime behavior or map scale and are flagged as such.
