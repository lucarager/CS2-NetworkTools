# Design: Handle Dependencies (`DependsOn`)

**Status:** Proposal

This document proposes replacing the `Parent` relationship on handle specs with a `DependsOn` list,
consolidating the several ad-hoc "follow" paths into one resolver + subscriber + per-spec dispatch,
and giving the rendering connector its own explicit declaration. It changes shared plumbing in
`NT_BaseToolSystem`, so it is a **behaviour-preserving** refactor: build via the solution and verify
each affected tool (Generate, Connect, RoadShape) in-game before and after.

### The motivating gap (read this first)

In the Generate tool's **Oval** mode there are three relevant parameters:

- `Position` — the oval origin. A `Float3Parameter` with a draggable `PositionHandle`.
- `Rotation` — the oval orientation. A `Float3Parameter` direction; **no handle**.
- `OvalRadiusZ` — the minor radius. A `FloatParameter` rendered as an **`AxisHandle`** whose axis is
  perpendicular to `Rotation`, anchored at `Position`. Its `StartPoint`/`EndPoint` delegates read
  both `Position.Value` and `Rotation.Value`
  ([GenerateToolSystem.cs:78-90](NetworkTools.Mod/Systems/Tools/Generate/GenerateToolSystem.cs:78)).

So the radius handle's geometry is a function of **two** inputs. But a handle spec can only declare
**one** `Parent`, and `OvalRadiusZ` declares `Parent = nameof(Position)`. The result: when the user
moves `Position`, the radius handle follows; when the user changes `Rotation`, the handle is left on
a **stale axis**. `Parent` structurally cannot express a multi-input dependency. That is the gap
this design closes — and it exposes that `Parent` is the wrong shape more generally (see §1).

---

## 1. What unifies, and what does not

It is tempting to look at all the "X follows Y" relationships and declare one grand mechanism. That
overshoots. The single thing common to every follow is the *compute shape* — a derived value
`target = f(inputs)`, resolved by walking specs and subscribing to their inputs. **That plumbing is
worth consolidating** (one resolver, one subscriber, one polymorphic dispatch; §4). But three things
that look mergeable are genuinely distinct, and this design keeps them separate on purpose:

- **Target: value vs entity.** Position-follow writes a *parameter value* (the model); circle/rotation
  recenter writes an *entity component* (the view). These are not interchangeable — **recenter has no
  parameter to write at all**: a circle handle's center comes from its anchor, there is no "center"
  parameter. That branch can never route through a value. A permanent boundary, not an implementation
  detail.
- **Trigger: value-change vs lifecycle.** A dependency recomputes when an *input value* changes.
  Parameter *seeding* (the `InitializeConfig` family, §3.3) fires at *lifecycle moments* (selection
  reaching Ready, mode change, a click). §5 shows these are not the same trigger and mostly should
  not be merged.
- **View: data vs rendering.** The dashed connector line between handles is a *view* relationship
  between handle **entities**; a dependency names **parameters**. §6 keeps the connector as its own
  explicit prop, deliberately **not** derived from `DependsOn`.

So the headline is **"consolidate the plumbing and fix the shape of the declaration,"** not "one
model for everything." `Parent` is dropped in favour of `DependsOn` for *all* follows — an
authoring-surface cleanup that also unblocks the multi-input case — while the spec *type* continues
to decide what each dependency actually does.

---

## 2. Glossary

- **Owner parameter** — the parameter a handle spec belongs to (its drag writes this).
- **Source / input** — a parameter (or non-parameter signal) the owner's value/geometry derives from.
- **Target** — what a dependency writes: the owner *parameter value* (model) or the *handle entity*
  components (view).
- **Trigger** — what causes a recompute: an input *value change*, or a *lifecycle moment*.

---

## 3. Current state (what must be preserved or replaced)

### 3.1 What `Parent` does today — three behaviours behind one keyword

`Parent` is a single `string` (a parameter name) on each `IHandleSpec`. What it *does* is dispatched
by the child spec's type:

| Child spec | Behaviour | Target | Math | Code |
|---|---|---|---|---|
| `Float3` `PositionHandle` | shift child **value** by parent delta | parameter value | delta (needs `LastParentPos`) | [Parameters.cs:102-115](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Parameters.cs:102) |
| `CircleHandle` / `RotationHandle` | recenter handle **entity** on parent | entity `NT_HandlePosition` | absolute copy | [Handles.cs `SyncParentPositionToChildHandles`](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:469) |
| `AxisHandle` / `ComputedPositionHandle` | re-resolve **entity** from delegates | entity pos + constraint | delegate | same method + `AxisHandle.SyncToEntity` |

Because the behaviour already comes from the spec type, the `Parent` *keyword* carries no
information that a `DependsOn` entry plus type-dispatch wouldn't. Three facts constrain any redesign:

- **The value path must write the *value*, not the entity.** The `Value` setter raises
  `ChangeOrigin.Code` ([Parameter.cs:19](NetworkTools.Mod/Systems/Tools/Parameters/Parameter.cs:19)),
  so writing a child's value cascades through *its own* reverse-sync (which moves its entity) and
  *its own* children (chaining). Moving only the entity would lose the model update and break chains.
- **Position-follow is delta, not absolute.** A child sits at an *offset* from its anchor; an
  absolute copy would snap it onto the anchor and destroy the offset. The delta needs remembered
  state (`LastParentPos`, held in `m_ParentChildLinks`). This state survives any redesign — it
  cannot be made stateless.
- **Reverse-sync (value → entity)** at [Parameters.cs:91-96](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Parameters.cs:91)
  skips `ChangeOrigin.Handle` (a drag already moved the entity).

### 3.2 The rendering relationship (`NT_HandleParent`)

- Emitted in `ResolveParentLinks` when the parent param has a handle entity
  ([Handles.cs:261-263](NetworkTools.Mod/Systems/Tools/Base/BaseToolSystem.Handles.cs:261)).
- Consumed by the overlay job to draw a **dashed connector line** from the child handle to the
  *parent handle entity*, **only for non-axis handles**
  ([DrawHandlesJob.cs:107-120](NetworkTools.Mod/Systems/Rendering/OverlaySystem.DrawHandlesJob.cs:107));
  axis/bezier handles instead draw their own origin→handle line from `constraints.Origin`
  ([DrawHandlesJob.cs:144-174](NetworkTools.Mod/Systems/Rendering/OverlaySystem.DrawHandlesJob.cs:144)).
- **This is orthogonal to the data dependency.** It is purely visual and needs the source's *handle
  entity* — but a dependency names a *parameter*. The resolver must bridge param → primary handle
  entity (as `ResolveParentLinks` already does). The connector cannot ride for free on `DependsOn`;
  it needs its own expression (§6).

### 3.3 The `InitializeConfig` family (parameter seeding)

Several tools imperatively seed parameters at lifecycle moments. This *looks* related to follows but
is a different trigger (§5):

| Tool | Method | What it seeds | Source | Trigger |
|---|---|---|---|---|
| Connect | `InitializeConfig` [Lifecycle.cs:21](NetworkTools.Mod/Systems/Tools/Connect/ConnectToolSystem.Lifecycle.cs:21) | `Start/EndPosition`, `Start/EndDirection` | **selected nodes + ECS topology** (non-param) | selection→Ready ([Update.cs:185](NetworkTools.Mod/Systems/Tools/Connect/ConnectToolSystem.Update.cs:185)), mode change ([Lifecycle.cs:151](NetworkTools.Mod/Systems/Tools/Connect/ConnectToolSystem.Lifecycle.cs:151)) |
| Connect | `SimpleCurveGenerator.Initialize` [:15](NetworkTools.Mod/Systems/Tools/Connect/Generators/SimpleCurveGenerator.cs:15) | 4 curve control points | **params** (Start/EndPos, Start/EndDir) | via `InitializeConfig` |
| Connect | `ComplexCurveGenerator.Initialize` [:14](NetworkTools.Mod/Systems/Tools/Connect/Generators/ComplexCurveGenerator.cs:14) | 4 control points + mid pos/rot | **params** (same 4 + derived bezier) | via `InitializeConfig` |
| Connect | `LoopGenerator.Initialize` [:13](NetworkTools.Mod/Systems/Tools/Connect/Generators/LoopGenerator.cs:13) | `LoopRadiusFactor=0.5`, `LoopArc=Outer` | **constants** | via `InitializeConfig` |
| Generate | `InitializeFromSecondPoint` [Update.cs:115](NetworkTools.Mod/Systems/Tools/Generate/GenerateToolSystem.Update.cs:115) | `GridDirectionPoint`, `OvalAxisPoint`, generator params | **second click point** (non-param) | hover/push/pop |
| RoadShape | `ShapeTransformContext.Create` [PathData.cs:159](NetworkTools.Mod/Systems/Tools/RoadShape/RoadShapeToolSystem.PathData.cs:159) | a *context struct* (not params) | path endpoints (non-param) | selection change |

---

## 4. Proposed model

### 4.1 `DependsOn` + polymorphic per-spec update

`Parent` is **removed**; every follow is a `DependsOn` entry. Each spec declares its inputs and the
spec *type* decides what an update does — the same dispatch that exists today, but named and in one
place.

```csharp
// On IHandleSpec — a sibling to the SyncToEntity each spec already implements.
public Dependency[] DependsOn { get; init; }

public readonly struct Dependency {
    public string           Source { get; }   // parameter name
    public DependencyUpdate Update { get; }   // null => spec-type default (OnDependencyChanged)
    public Dependency(string source, DependencyUpdate update = null) { Source = source; Update = update; }
    public static implicit operator Dependency(string source) => new(source);   // bare-name sugar
}

// Custom per-source reaction (see §4.4); null Update => use the spec-type default below.
public delegate void DependencyUpdate(NT_BaseToolSystem tool, ParameterBase owner, ParameterBase source);

// Spec-type default, used for every bare (Update == null) entry:
void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                         ParameterBase owner, Float3Parameter source, float3 delta);
```

A **bare** entry (just a source name — `Dependency` converts implicitly from `string`, so the common
case stays a terse list) runs the spec-type default `OnDependencyChanged`:

- **`PositionHandle` (Float3 owner)** → `owner.Value += delta;` — automatic position-to-position
  follow; the value-write cascades (reverse-sync + grandchildren) for free.
- **`CircleHandle` / `RotationHandle`** → recenter the entity on `source.Value`.
- **`AxisHandle` / `ComputedPositionHandle`** → `SyncToEntity(...)` (re-resolve position + constraint
  from the spec's delegates). These accept **multiple** inputs by listing them all in `DependsOn` —
  this is what closes the motivating gap: `OvalRadiusZ.DependsOn = new Dependency[]{ nameof(Position), nameof(Rotation) }`,
  so the radius handle re-resolves when *either* the origin or the orientation changes.

An entry that carries an `Update` delegate runs that instead (custom relationships, §4.4).

`Parent = nameof(Position)` on a Float3 `PositionHandle` becomes
`DependsOn = new Dependency[]{ nameof(Position) }`; the spec type supplies the follow behaviour, so
the keyword is redundant and is deleted outright.

**Single-anchor invariant.** `Parent` was *singular*; `DependsOn` is a list. The follow
(`PositionHandle`) and recenter (`Circle`/`Rotation`) behaviours only make sense with **one** anchor
(you cannot translate by two deltas, or center on two points), and every such handle in the codebase
has exactly one *default* source today. Only the recompute kind (`Axis`/`Computed`) is genuinely
multi-source, and it re-resolves from *all* its inputs and needs no anchor. So enforce: **follow- and
recenter-kind specs take at most one *bare* (delegate-less) entry** (assert at resolve time);
recompute-kind specs, and any entries carrying a custom `Update` delegate (§4.4), take any number.
This makes explicit the guarantee `Parent`'s singularity gave for free.

### 4.2 State and triggers

- A single resolver builds `Dictionary<ParameterBase, List<(Entity, IHandleSpec, ownerParam)>>`
  per mode (like the existing `m_ParameterHandles`), plus the per-link `LastParentPos` for the delta
  (follow) specs — i.e. `m_ParentChildLinks`, renamed and absorbed into the one structure.
- One persistent subscriber per parameter (a third block alongside the two already in
  `WireParameterSubscribers`): on change, for each dependent run its entry's `Update` delegate, or
  the spec-type `OnDependencyChanged` for a bare entry. Dependents react regardless of the *source's*
  change origin (a follow tracks its anchor whether it moved by drag or by code). Cycles and re-entry
  are bounded by the per-pass visited set (§4.4) plus the `LastParentPos` delta no-op; the separate
  owner reverse-sync (§3.1) still skips the owner's own `Handle` origin.
- **The trigger is strictly a parameter value-change.** `DependsOn` names parameters only;
  dependencies on non-parameter state (selection, phase, clicks) are deliberately *not* supported
  and stay in the lifecycle seeders (§5, §9). Both `DependsOn` and `OnDependencyChanged` live on
  `IHandleSpec` uniformly (beside `SyncToEntity`); a null `DependsOn` means no dependencies.

### 4.3 "Derived-yet-editable" is already solved — without a latch

A natural worry: these params are seeded/followed from inputs yet are also user-draggable, so won't a
recompute clobber the user's edit, requiring a dirty/override latch? For every case in the codebase,
**no latch is needed**, because the follows are all *translation*, and delta-follow handles
translation losslessly:

- The delta path reconstructs the offset as `(child − parent)` on every parent move via
  `LastParentPos`. When the user re-offsets the child (drag → `ChangeOrigin.Handle`), `LastParentPos`
  is untouched, so the next parent move applies the delta to the *new* dragged value — the user's
  offset is preserved with no flag. The `OvalAxisPoint`-follows-`Position` relationship is exactly
  this.
- The recompute case (`OvalRadiusZ`) re-projects the **same** radius value onto the new axis when
  `Rotation` changes — it preserves the user's value by construction, not by suppression.

A dirty/override latch would only be justified for a **non-translation, derived-yet-editable** param
whose inputs change *during* editing and that delta-follow cannot express. No such param exists in
the current tools. Because a latch is the most fragile thing one could add here and nothing motivates
it, the seeding fold that would need it is quarantined in **§10**, gated on first exhibiting such a
param.

### 4.4 Custom relationships and cycle safety

The spec-type default covers the common flows with zero ceremony (`DependsOn = new Dependency[]{ nameof(Position) }`).
For a relationship the default cannot express — e.g. **tangent (G1) continuity** between two bezier
control points that must stay collinear through their shared node — attach an `Update` delegate to
the specific source that needs it. Resolution is **per source**, not per handle: this matters because
different sources of the same handle usually want *different* reactions.

Concretely, the two control points should **mirror** each other through the node when one is dragged,
but **translate** rigidly when the node itself moves. A single delegate for all sources gets the node
case wrong (reflecting through a moved node shoves one point by 2× the node's delta and leaves the
other in place). Per-source resolution declares each reaction where it belongs:

```csharp
// Node moves  → both CPs translate with it (BARE entry → spec default = PositionHandle translate).
// Drag one CP → the other mirrors through the node (CUSTOM entry → delegate).
CP_A: DependsOn = new Dependency[]{ nameof(Node), new(nameof(CP_B), MirrorThroughNode) }
CP_B: DependsOn = new Dependency[]{ nameof(Node), new(nameof(CP_A), MirrorThroughNode) }
```

The bare `Node` entry rigidly translates the control point (and translating all three points by the
same delta keeps them collinear for free). The `MirrorThroughNode` delegate reads what it needs from
`tool` and writes `owner.SetValue(mirrored, ChangeOrigin.Dependency)`; it fires only when the sibling
changes. Custom (delegate-bearing) entries do **not** count against the single-anchor invariant
(§4.1) — only bare entries do — so a follow/recenter handle may still carry any number of them.

**Cycle safety — a per-pass visited set.** A mutual dependency is a cycle (A→B→A), so a naive
subscriber loops. Guard the propagation with a visited set seeded by the **root** (user-changed)
parameter, and update each parameter at most once per pass:

- Drag `CP_A` (origin `Handle`) → pass starts, visited = `{CP_A}`.
- `CP_B` depends on `CP_A` → not visited → run its update → add `CP_B` → its change fires.
- `CP_A` depends on `CP_B` → already visited → **skip**. Pass ends.

This terminates for any cycle topology and any delegate (no need to prove the math is an involution),
while still letting acyclic chains cascade fully (each node updated once). It also resolves *who
wins*: the dragged parameter is the root and is never overwritten by the bounce-back — drag `CP_A`,
`CP_B` follows, `CP_A` stays put. Add a `ChangeOrigin.Dependency` for dependency-driven writes so
they are distinguishable; the visited set is the primary guard, with the existing
`lengthsq(delta) < ε` no-op check as a cheap early-out.

**Limitation:** the visited set guarantees *termination*, not multi-source *fixpoint ordering* — a
node that convergently depends on two sources updated in the same pass sees whichever fired first.
No current or sketched relationship needs that; flag it if one arises.

---

## 5. Why `InitializeConfig` does **not** fold into `DependsOn`

Parameter seeding looks foldable but is a different trigger. Case by case:

| Seeding work | Verdict |
|---|---|
| **Connect `SimpleCurve` / `ComplexCurve` control points** (= f of params) | ⚠️ **Do not fold (see §10).** It *looks* like a `DependsOn` win, but the inputs (`Start/EndPosition`, `Start/EndDirection`) change **only inside `InitializeConfig`** — never during control-point editing. A value-triggered dep would therefore fire at exactly the moments the lifecycle hook already fires and never in between: it buys nothing over the hook while adding a latch to suppress a recompute that never happens. Worse, at re-selection the current behaviour *deliberately* re-seeds (clobbers) — so a latch would guard the one moment you don't want it to. Keep the imperative `Generator.Initialize`. |
| **Connect `Start/EndPosition`, `Start/EndDirection`** (= f of selected nodes + ECS topology) | ❌ **Keep imperative.** Source is the *selection*, not a parameter, and `ComputeNodeDirection` reads `Edge`/`Curve`/`ConnectedEdge` — that does not belong in a declarative spec delegate. Needs a **lifecycle trigger** (selection→Ready), not a value trigger. |
| **Connect `LoopRadiusFactor` / `LoopArc`** (constants) | ⚠️ **Re-express as `Default` + lifecycle reset.** No source param. Today they re-seed on every `InitializeConfig`; model that as "reset to default on (re)enter Ready," not a dependency. |
| **Generate `Position`, `GridDirectionPoint`, `OvalAxisPoint`, generator seeds** (= f of click points) | ❌ **Keep imperative.** Source is the raycast/click, not a parameter. |
| **RoadShape `ShapeTransformContext`** | ❌ **Not parameters.** A Burst-built context struct the AxisHandles read via delegates; out of scope for a parameter-dependency model. |

**Conclusion:** seeding is either (a) selection/click-derived — non-parameter source, needs the
lifecycle hook; (b) constant resets — belong in `Default`; or (c) param-derived but with inputs that
are *static during editing*, so a value-trigger is redundant with the lifecycle hook it would
replace. `InitializeConfig` stays as the **lifecycle-triggered** seeder, unchanged. The only
genuinely value-triggered relationships are the geometry follows (position-follow, `OvalRadiusZ`) —
which is exactly what §4's plumbing serves. There is no shared "compute + latch core" to build.

---

## 6. Rendering: an explicit connector prop

The dashed connector is a *view* relationship between handle **entities**; `DependsOn` is a *data*
relationship between **parameters**. **These must not be coupled** — the connector is *not* derived
from `DependsOn`. Auto-emitting a tether from the data dependency would re-mix the two concerns this
design separates, and would force a fragile "which dependency is the visual anchor?" heuristic onto
multi-input specs.

Instead, add a dedicated, optional rendering prop on `IHandleSpec`:

```csharp
// Parameter name whose primary handle entity this handle draws a connector line to.
// Render-only; has no effect on value or geometry. Null = no connector.
public string RenderConnectionTo { get; init; }
```

- It is a `string` parameter-name, resolved like the existing `ConstraintOriginFrom` / `NormalFrom`
  references, so it fits current authoring style: `RenderConnectionTo = nameof(Position)`.
- The resolver bridges name → primary handle entity and stamps a pure-rendering component (rename
  `NT_HandleParent` → `NT_HandleConnector`, holding that target entity). The draw job is unchanged
  apart from the component name.
- A handle may declare `RenderConnectionTo` independently of any `DependsOn` (and vice-versa) — e.g.
  `OvalAxisPoint` would carry both `DependsOn = new Dependency[]{ nameof(Position) }` (data: follow) and
  `RenderConnectionTo = nameof(Position)` (view: tether), expressed separately and on purpose.

To preserve current visuals, port today's behaviour explicitly: handles that had `Parent` and were
*not* axis/bezier (which draw their own origin line) get `RenderConnectionTo` set to their former
parent; axis/bezier handles do not.

---

## 7. Migration

All steps are **behaviour-preserving** and need no override latch. Each is independently verifiable;
verify the affected tools (Generate, Connect, RoadShape) in-game after each.

1. **(Optional interim) Close the radius gap cheaply.** Introduce `DependsOn` as an *additive*
   recompute trigger only — `OvalRadiusZ.DependsOn = new Dependency[]{ nameof(Rotation) }` (it already declares
   `Position` as its parent) — so the axis handle re-resolves when `Rotation` changes. Re-projects
   the same radius value onto the new axis, so no latch. This coexists with `Parent` and ships the
   visible fix ahead of the larger change.
2. **Core plumbing + drop `Parent`.** Add `OnDependencyChanged` and the single dependency
   resolver/subscriber; replace every `Parent` with a `DependsOn` entry and **delete the `Parent`
   prop**. Assert the single-anchor invariant (§4.1) on follow/recenter specs. Verify all former
   parent-follow, circle/rotation recenter, and axis cases are unchanged. Retain the `LastParentPos`
   delta guard.
3. **Rendering.** Add `RenderConnectionTo` (§6), rename `NT_HandleParent` → `NT_HandleConnector`, and
   set `RenderConnectionTo` on the handles that previously relied on `Parent` for their tether.
   Explicitly decoupled from `DependsOn`.

The seeding fold (§10) is **out of scope** and ships only if it clears its burden of proof.

---

## 8. Risks

- **Trigger origin policy** — mixing all-origin (follow) and skip-`Handle` (recompute) under one
  subscriber must not create feedback loops; the `LastParentPos` delta guard must be retained.
- **Burst / job boundary** — RoadShape's transform context is built in a Burst job; do not try to
  pull that into managed dependency delegates.
- **Shared plumbing** — this changes `NT_BaseToolSystem`'s handle/parameter resolution, which every
  tool uses. Lean on in-game verification per tool; the change has no unit-test coverage.

---

## 9. Decisions

1. **`Parent` is dropped** in favour of `DependsOn` for all follows; the spec type supplies the
   behaviour (§4.1).
2. **The rendering connector is explicit** — a `RenderConnectionTo` prop, deliberately *not* derived
   from `DependsOn` (§6).
3. **Trigger scope: parameter value-change only.** `DependsOn` is strictly parameter → parameter.
   The trigger is *not* generalized to non-parameter signals (selection, phase, clicks): those
   recompute heavy ECS-topology logic (e.g. `ComputeNodeDirection` reading `Edge`/`Curve`/
   `ConnectedEdge`) that does not belong in a declarative spec, and folding them in would erase the
   value-change-vs-lifecycle trigger boundary (§1). Selection/click-derived seeding stays imperative
   in the lifecycle hook (`InitializeConfig`, §5).
4. **`DependsOn` exposure: uniform on `IHandleSpec`.** Put `DependsOn` + `OnDependencyChanged` on the
   interface beside `SyncToEntity`. "Only where follows exist" is in fact every spec type (follow /
   recenter / recompute), so uniform is the *smaller* surface and the single resolver path; a null
   `DependsOn` means no dependencies.
5. **Single-anchor invariant: enforced by a resolve-time assert + error log,** not a compile-time
   field. A separate single-source field on follow/recenter specs would just be `Parent` under a new
   name, re-adding the surface this design removes. Keep one `Dependency[] DependsOn` everywhere; in
   the dependency-map build (runs on handle rebuild at mode entry), a follow/recenter-kind spec with
   more than one *bare* (delegate-less) entry is a programming error → assert + log, take the first.
   Custom (delegate-bearing) entries don't count. It surfaces deterministically the first time the
   tool's handles build.
6. **Custom relationships via per-source `Update` delegates** (§4.4). `DependsOn` is a `Dependency[]`;
   a bare entry uses the spec-type default, an entry carrying an `Update` delegate uses that instead —
   so different sources of one handle can react differently (e.g. node→translate, sibling→mirror). An
   implicit `string`→`Dependency` conversion keeps the simple case a terse list. Custom entries are
   exempt from the single-anchor invariant. **Cycles are made safe by a per-pass visited set** seeded
   with the root (user-changed) parameter, with a dedicated `ChangeOrigin.Dependency` for
   dependency-driven writes.

---

## 10. Quarantined: seeding fold + override latch (do not ship without proof)

A tempting extension, recorded here so the idea isn't silently lost — but **not recommended**.

**The idea.** Move Connect `SimpleCurve`/`ComplexCurve` control-point seeding out of
`Generator.Initialize` into declarative `DependsOn = [Start/EndPosition, Start/EndDirection]` +
compute, with a per-parameter **dirty/override latch** (clean → recompute; user drag → dirty, stop
recomputing; explicit re-init → clear) so a value-triggered recompute doesn't clobber edits.

**Why it's quarantined.**
- The seed inputs change *only* at `InitializeConfig` time (selection→Ready, mode change), never
  during editing — so a value-trigger fires at the same moments the lifecycle hook already does and
  is redundant with it. The latch would then exist solely to suppress a recompute that never occurs.
- At re-selection the current behaviour *intends* to re-seed/clobber; a latch would fight that.
- Translation is already latch-free via delta-follow (§4.3). A latch is the most fragile element one
  could add and would impose a clean→dirty→clear state machine on every derived param.

**Burden of proof before this ships.** Exhibit a real parameter that is *all* of: (a) derived from
other parameters via a **non-translation** function (so delta-follow can't express it), (b)
**user-editable**, and (c) has inputs that change **during editing** (not just at a lifecycle
moment). No such parameter exists in the current tools. Absent one, keep `InitializeConfig` as the
lifecycle-triggered seeder and do not build the latch.

**Milder residue (also not prioritized).** If the only goal is deleting the imperative
`Generator.Initialize` bodies, express the seed *formula* declaratively but invoke it from the
**lifecycle hook** — no value-trigger, no latch. Marginal cleanup; the imperative versions read
fine, so this is low value.
