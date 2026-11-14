namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    ///     Connects two nodes with <b>strictly circular</b> arcs that are tangent to both node tangents.
    ///     <para>
    ///         Each shape is built from the same three primitives: a circle tangent to the two node
    ///         tangents, straight runs along those tangents, and a vertical profile that spends the
    ///         height difference at the design grade. The two nodes stay exactly where the user picked
    ///         them; the arcs sit between the tangent points instead, so tangency is exact by
    ///         construction rather than by fitting.
    ///     </para>
    ///     <para>
    ///         Four shapes exist, in two families:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <b>Intersecting tangents</b> -- <see cref="LoopShape.Orthogonal" /> (arc cuts the
    ///             corner, 90° on a right angle) and <see cref="LoopShape.Crossing" /> (arc goes the long
    ///             way round, 270°). Solved through <c>C(r) = cBase + r * cDir</c>: the centre moves
    ///             linearly with the radius along the corner bisector, so the feasible radius band comes
    ///             from two linear inequalities and the factor maps onto it.
    ///         </item>
    ///         <item>
    ///             <b>Parallel tangents</b> -- <see cref="LoopShape.UTurn" /> (roads run opposite ways:
    ///             one 180° half circle, radius pinned to <c>|AB| / 2</c>) and
    ///             <see cref="LoopShape.Stacked" /> (roads run the same way: two 270° loops joined by a
    ///             level deck). Neither is reachable from the corner solver, so both get their own
    ///             closed-form construction.
    ///         </item>
    ///     </list>
    /// </summary>
    public struct LoopGenerator : IConnectionGenerator {
        /// <summary>Radius below which the arc degenerates into a visible corner.</summary>
        private const float kMinRadius = 4f;

        /// <summary>
        ///     Below this |determinant| the two tangent lines are treated as parallel. Their normals
        ///     then span no 2D basis, so the linear solve for the centre is undefined.
        /// </summary>
        private const float kParallelEpsilon = 1e-3f;

        /// <summary>Lead-ins shorter than this are dropped instead of emitted as sliver edges.</summary>
        private const float kMinLeadIn = 0.05f;

        /// <summary>Cosine threshold for "the two tangents point the same way" / "opposite ways".</summary>
        private const float kParallelDot = 0.999f;

        /// <summary>
        ///     How far a node has to be off the terrain before it is announced as a bridge or a cutting.
        ///     Anything inside this band is left to follow the ground, so a loop that merely kisses the
        ///     surface is not turned into a two-lane viaduct.
        /// </summary>
        private const float kGroundTolerance = 0.75f;

        public static void Initialize(NT_ConnectToolSystem tool) {
            tool.LoopRadiusFactor.Value = 0.5f;
            tool.LoopShapeMode.Value    = LoopShape.Orthogonal;
            tool.LoopGrade.Value        = 0.04f;
            tool.LoopHeight.Value       = 0f;
        }

        public void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            if (!TrySolve(config, out var solution)) return;

            Emit(in config, in solution, ref curves);
        }

        // ── Solving ─────────────────────────────────────────────────────────────

        /// <summary>Which construction produced the solution -- it decides how the edges are emitted.</summary>
        public enum LoopKind {
            /// <summary>One arc, optionally with straight lead-ins along the node tangents.</summary>
            Single = 0,

            /// <summary>One 180° arc between two parallel nodes, no lead-ins.</summary>
            UTurn = 1,

            /// <summary>Two 270° arcs joined by a straight elevated deck.</summary>
            Stacked = 2,
        }

        /// <summary>Result of a successful loop solve, in the XZ plane.</summary>
        public struct LoopSolution {
            public LoopKind Kind;

            /// <summary>First arc. For <see cref="LoopKind.Stacked" /> this is the loop leaving START.</summary>
            public float2 Center;
            public float  Radius;
            public float2 ArcStart;
            public float2 ArcEnd;
            public float  StartOffset;
            public float  EndOffset;

            /// <summary>Signed sweep in radians. Positive is counter-clockwise in XZ.</summary>
            public float Sweep;

            public bool Ccw;

            /// <summary>Stacked only: centre of the loop arriving at END.</summary>
            public float2 Center2;

            /// <summary>Stacked only: where the second loop starts, i.e. the far end of the deck.</summary>
            public float2 DeckEnd;

            /// <summary>Stacked only: where the second loop lands (END itself unless a lead-out follows).</summary>
            public float2 ArcEnd2;

            /// <summary>Stacked only: sweep of the second loop (same magnitude and sense as the first).</summary>
            public float Sweep2;

            public bool Valid;
        }

        private struct Candidate {
            public float2 Center;
            public float  Radius;
            public float2 ArcStart;
            public float2 ArcEnd;
            public float  StartOffset;
            public float  EndOffset;
            public float  Sweep;
            public bool   Ccw;
            public bool   Valid;

            /// <summary>
            ///     True when the centre sits inside the corner the two leads enclose, i.e. the arc cuts
            ///     the corner instead of going round the outside. Always the shorter of the two sweeps.
            /// </summary>
            public bool IsShort;
        }

        /// <summary>
        ///     Solves the requested shape. Returns false -- and the caller therefore emits nothing --
        ///     when that shape does not fit this node pair.
        /// </summary>
        public static bool TrySolve(in ConnectJobConfig config, out LoopSolution solution) {
            solution = default;

            float2 a  = config.StartPosition.xz;
            float2 da = math.normalizesafe(new float2(config.StartDirection.x, config.StartDirection.z));
            float2 b  = config.EndPosition.xz;

            // The tool-wide convention is that the path *arrives* at B travelling along
            // -EndDirection: SimpleCurveGenerator places the last bezier control point at
            // End + EndDirection * len, so d - c points opposite to EndDirection.
            // LoopGenerator used to feed +EndDirection in here, which demanded that the arc
            // leave B the same way the node's own road leaves it. Only the branches whose
            // centre sits on the near side of B survived that test, so the requested arc got
            // pushed into the adjacent quadrant and the lead-out doubled back over the node.
            float2 db = math.normalizesafe(new float2(-config.EndDirection.x, -config.EndDirection.z));

            if (math.lengthsq(da) < 0.5f || math.lengthsq(db) < 0.5f) return false;
            if (math.distance(a, b) < 1e-3f) return false;

            float factor = math.saturate(config.LoopRadiusFactor);

            // The two parallel families get their own closed form: with parallel tangents the
            // four-branch linear solve below has no solution at all (det == 0).
            if (config.Shape == LoopShape.UTurn) return TrySolveUTurn(a, da, b, db, out solution);
            if (config.Shape == LoopShape.Stacked) return TrySolveStacked(a, da, b, db, factor, out solution);

            // All four tangent-circle branches are solved up front; which one wins is decided by the
            // arc sweep alone -- see SelectShape / LoopShape.
            var ll = SolveBranch(a, da, b, db, +1, +1, factor);
            var lr = SolveBranch(a, da, b, db, +1, -1, factor);
            var rl = SolveBranch(a, da, b, db, -1, +1, factor);
            var rr = SolveBranch(a, da, b, db, -1, -1, factor);

            var chosen = SelectShape(config.Shape, in ll, in lr, in rl, in rr);
            if (!chosen.Valid) return false;

            solution = new LoopSolution {
                Kind        = LoopKind.Single,
                Center      = chosen.Center,
                Radius      = chosen.Radius,
                ArcStart    = chosen.ArcStart,
                ArcEnd      = chosen.ArcEnd,
                StartOffset = chosen.StartOffset,
                EndOffset   = chosen.EndOffset,
                Sweep       = chosen.Sweep,
                Ccw         = chosen.Ccw,
                Valid       = true,
            };
            return true;
        }

        // ── Parallel family ─────────────────────────────────────────────────────

        /// <summary>Left normal in the XZ plane: with <c>x</c> east and <c>y</c> north, north's left is west.</summary>
        private static float2 Left(float2 v) => new float2(-v.y, v.x);

        /// <summary>
        ///     Splits the <b>fore-aft</b> offset of B relative to A into a straight run, so the loop
        ///     construction only ever sees two nodes that are exactly abeam.
        ///     <para>
        ///         Both parallel shapes are closed form only when <c>B - A</c> is perpendicular to the
        ///         tangents: one free radius cannot absorb two offsets at once. A node pair that is
        ///         staggered along the road is not a different problem though -- the stagger is simply
        ///         driven out along the tangent first, which is exactly what a real approach does. When
        ///         B is ahead of A the run becomes a lead-in (START side); when it is behind, a lead-out
        ///         (END side). Either way <c>b2 - a2</c> ends up purely lateral.
        ///     </para>
        /// </summary>
        private static void SplitForeAft(
            float2 a, float2 da, float2 b, float fore,
            out float2 a2, out float2 b2,
            out float startOffset, out float endOffset) {
            startOffset = math.max(0f, fore);
            endOffset   = math.max(0f, -fore);
            a2          = a + da * startOffset;
            b2          = b + da * endOffset;
        }

        /// <summary>
        ///     Two side-by-side parallel roads, tucked back with a single 180° half circle.
        ///     <para>
        ///         Both nodes are pinned and both tangents are parallel, so the circle has to be tangent
        ///         to two parallel lines at the two nodes -- which forces the nodes to be diametrically
        ///         opposite: the centre is their midpoint and the radius is <c>|AB| / 2</c>. There is no
        ///         free parameter and no second solution (the other half of the same circle would leave
        ///         A backwards), which is why the U-turn ignores the radius factor. This is the tighter of
        ///         the two parallel shapes; <see cref="TrySolveStacked" /> is the same node pair with a
        ///         free radius and an elevated deck instead.
        ///     </para>
        /// </summary>
        private static bool TrySolveUTurn(
            float2 a, float2 da, float2 b, float2 db, out LoopSolution solution) {
            solution = default;

            if (math.dot(da, db) > -kParallelDot) return false;

            float fore = math.dot(b - a, da);
            SplitForeAft(a, da, b, fore, out float2 a2, out float2 b2,
                out float startOffset, out float endOffset);

            float lateral = math.dot(b2 - a2, Left(da));
            float radius  = math.abs(lateral) * 0.5f;
            if (!(radius > 1f) || !math.isfinite(radius)) return false;

            float2 center = (a2 + b2) * 0.5f;
            float2 ra     = a2 - center;
            float  theta0 = math.atan2(ra.y, ra.x);

            // Travel sense: whichever direction along the circle heads out of A along da.
            bool  ccw    = math.dot(Left(ra), da) > 0f;
            float sweep  = ccw ? math.PI : -math.PI;
            float theta1 = theta0 + sweep;

            var    dir    = new float2(math.cos(theta1), math.sin(theta1));
            float2 arcEnd = center + radius * dir;
            if (math.distance(arcEnd, b2) > math.max(radius * 2e-3f, 0.05f)) return false;

            float2 tanEnd = ccw ? Left(dir) : -Left(dir);
            if (math.dot(tanEnd, db) < kParallelDot) return false;

            solution = new LoopSolution {
                Kind        = LoopKind.UTurn,
                Center      = center,
                Radius      = radius,
                ArcStart    = a2,
                ArcEnd      = b2,
                StartOffset = startOffset,
                EndOffset   = endOffset,
                Sweep       = sweep,
                Ccw         = ccw,
                Valid       = true,
            };
            return true;
        }

        /// <summary>
        ///     The same pair of side-by-side parallel roads, but taken the long way: leave START, loop
        ///     270° away from the other road and down to a deck, run across the gap level, loop 270°
        ///     again and drop into END.
        ///     <para>
        ///         Unlike the half circle this one has a <b>free radius</b>, so it is what you reach for
        ///         when the gap is too narrow (or the height difference too large) for a U-turn: a bigger
        ///         radius buys longer ramps, which is exactly what the design grade needs.
        ///     </para>
        ///     <para>
        ///         Everything is closed form. Writing <c>n</c> for the unit normal from A towards the
        ///         first centre and solving for "the second loop must land exactly on B" gives
        ///             <c>deck = 2r - (B - A)·n</c>
        ///         so the deck length is <c>2r + |lateral|</c>: always positive, and always long enough
        ///         that the two loops cannot overlap each other, whatever the radius.
        ///     </para>
        ///     <para>
        ///         The loops must go round on the side <b>away</b> from the other road, otherwise the
        ///         short deck would fold them back over each other. That fixes both the centre side and
        ///         the rotational sense -- there is exactly one construction per radius.
        ///     </para>
        /// </summary>
        private static bool TrySolveStacked(
            float2 a, float2 da, float2 b, float2 db, float factor, out LoopSolution solution) {
            solution = default;

            // The link doubles back: it leaves START one way and reaches END travelling the
            // opposite way, because both nodes are the *far* ends of two side-by-side roads.
            // The second loop exits along -da by construction, so that is the gate here.
            if (math.dot(da, db) > -kParallelDot) return false;

            float fore = math.dot(b - a, da);
            SplitForeAft(a, da, b, fore, out float2 a2, out float2 b2,
                out float startOffset, out float endOffset);

            float2 nL      = Left(da);
            float  lateral = math.dot(b2 - a2, nL);
            if (math.abs(lateral) < 1e-3f) return false;

            // Centre side: away from B, so the deck stays long (see the summary above).
            int    sense = lateral < 0f ? +1 : -1;
            float2 n     = nL * sense;

            float lo     = kMinRadius;
            float hi     = math.max(lo * 3f, math.abs(lateral) * 1.5f + 20f);
            // Whole metres, same rule as the corner solver -- the two loops share this radius.
            float radius = math.clamp(math.round(math.lerp(lo, hi, factor)), lo, hi);
            if (!math.isfinite(radius) || radius <= 0f) return false;

            // ── First loop: leaves A along da, exits travelling along -n ──
            float2 c1     = a2 + n * radius;
            float  theta0 = math.atan2(a2.y - c1.y, a2.x - c1.x);
            float  sweep  = sense * (1.5f * math.PI);
            float  theta1 = theta0 + sweep;

            var    dir1 = new float2(math.cos(theta1), math.sin(theta1));
            float2 e1   = c1 + radius * dir1;
            float2 t1   = sense * Left(dir1); // exit tangent == -n

            float deck = 2f * radius - math.dot(b2 - a2, n);
            if (!(deck > 0f) || !math.isfinite(deck)) return false;

            // ── Deck, then the second loop lands on B ──
            float2 s2   = e1 + t1 * deck;
            float2 c2   = s2 + sense * Left(t1) * radius;
            float  phi0 = math.atan2(s2.y - c2.y, s2.x - c2.x);
            float  phi1 = phi0 + sweep;
            var    dir2 = new float2(math.cos(phi1), math.sin(phi1));
            float2 e2   = c2 + radius * dir2;

            if (!math.all(math.isfinite(e2)) || !math.all(math.isfinite(c2))) return false;
            if (math.distance(e2, b2) > math.max(radius * 2e-3f, 0.05f)) return false;

            solution = new LoopSolution {
                Kind        = LoopKind.Stacked,
                Center      = c1,
                Radius      = radius,
                ArcStart    = a2,
                ArcEnd      = e1,
                Center2     = c2,
                DeckEnd     = s2,
                ArcEnd2     = b2,
                StartOffset = startOffset,
                EndOffset   = endOffset,
                Sweep       = sweep,
                Sweep2      = sweep,
                Ccw         = sense > 0,
                Valid       = true,
            };
            return true;
        }

        /// <summary>
        ///     Picks the branch that matches the requested shape.
        ///     <para>
        ///         Orthogonal wants the arc that cuts the corner -- its centre lies in the wedge the two
        ///         leads enclose, which is always the shorter of the two sweeps. Crossing wants the
        ///         centre in the opposite corner, which is always the longer one. The two solutions of a
        ///         mirror pair add up to a full turn, so |sweep| alone distinguishes them and no
        ///         per-quadrant parameter is needed.
        ///     </para>
        ///     <para>
        ///         Within a kind the smaller sweep wins; on a tie, less total lead-in. There is
        ///         deliberately no fallback to the other kind: if the requested shape cannot be built
        ///         for this node pair the generator emits nothing, so the preview just stays empty
        ///         instead of silently swapping a 270° trumpet for a 90° fillet.
        ///     </para>
        /// </summary>
        private static Candidate SelectShape(
            LoopShape        shape,
            in Candidate ll, in Candidate lr,
            in Candidate rl, in Candidate rr) {
            bool wantShort = shape == LoopShape.Orthogonal;

            var best = default(Candidate);
            var have = false;

            Consider(in ll, wantShort, ref best, ref have);
            Consider(in lr, wantShort, ref best, ref have);
            Consider(in rl, wantShort, ref best, ref have);
            Consider(in rr, wantShort, ref best, ref have);
            return best;
        }

        /// <summary>
        ///     Keeps whichever candidate has the smaller arc; on a tie the one with less total lead-in
        ///     (so the geometry stays as close to the nodes as possible). Candidates of the other kind
        ///     are ignored outright.
        /// </summary>
        private static void Consider(
            in Candidate c, bool wantShort,
            ref Candidate best, ref bool have) {
            if (!c.Valid) return;
            if (c.IsShort != wantShort) return;

            if (!have) {
                best = c;
                have = true;
                return;
            }

            float sweepC = math.abs(c.Sweep);
            float sweepB = math.abs(best.Sweep);

            if (sweepC < sweepB - 1e-4f
                || (math.abs(sweepC - sweepB) <= 1e-4f
                    && c.StartOffset + c.EndOffset < best.StartOffset + best.EndOffset)) {
                best = c;
            }
        }

        /// <summary>
        ///     Solves one of the four branches. <paramref name="sA" /> / <paramref name="sB" /> are the
        ///     signed sides of the start / end tangent lines the centre lies on.
        /// </summary>
        private static Candidate SolveBranch(
            float2 a, float2 da, float2 b, float2 db,
            int sA, int sB, float factor) {
            var result = default(Candidate);

            float2 na  = new float2(-da.y, da.x);
            float2 nb  = new float2(-db.y, db.x);
            float  det = na.x * nb.y - na.y * nb.x;

            // ── Parallel tangents ────────────────────────────────────────────────
            // No corner circle exists here; the parallel family (UTurn / Stacked) is solved
            // separately and never through this branch. Returning invalid keeps the corner solver
            // from inventing a circle of unbounded radius out of a near-zero determinant.
            if (math.abs(det) <= kParallelEpsilon) return result;

            // ── Intersecting tangents ────────────────────────────────────────────
            // Both signed-distance equations are linear in (C, r):
            //   (C - a)·na = sA·r ,  (C - b)·nb = sB·r
            // hence C(r) = cBase + r·cDir.
            float  ka    = math.dot(na, a);
            float  kb    = math.dot(nb, b);
            float2 cBase = new float2((ka * nb.y - na.y * kb) / det,
                                      (na.x * kb - ka * nb.x) / det);
            float2 cDir  = new float2((sA * nb.y - sB * na.y) / det,
                                      (sB * na.x - sA * nb.x) / det);

            float tA0 = math.dot(cBase - a, da), tA1 = math.dot(cDir, da);
            // The end tangent point sits *behind* B (on the -db side), so that the path
            // arrives at B travelling along db -- i.e. it hands over to the existing road
            // in the same direction the road continues.
            float tB0 = math.dot(b - cBase, db), tB1 = -math.dot(cDir, db);

            float lo = kMinRadius;
            float hi = float.PositiveInfinity;
            if (!ApplyNonNegative(tA0, tA1, ref lo, ref hi)) return result;
            if (!ApplyNonNegative(tB0, tB1, ref lo, ref hi)) return result;
            if (lo > hi) return result;

            if (!math.isfinite(hi)) {
                // Unbounded above: cap it at a radius that still looks like a loop.
                hi = math.max(lo * 4f, math.max(lo, math.distance(a, b) * 1.5f));
            }
            if (hi <= lo) hi = lo + 1f;

            // Whole-metre arcs. The player reads and thinks in metres, so the *geometry* is what gets
            // quantized, not the 0..1 knob: `radius` is linear in the factor inside a branch, so
            // rounding here makes the knob step through 12 m, 13 m, 14 m ... instead of a continuum
            // nobody can hit twice. Clamped to the branch's own feasible band so rounding can never
            // push the circle outside the interval where the tangency solve holds.
            float2 center;
            float  radius = math.clamp(math.round(math.lerp(lo, hi, factor)), lo, hi);
            center = cBase + cDir * radius;

            if (!math.isfinite(radius) || !math.all(math.isfinite(center))) return result;

            // Perpendicular feet of the centre on each tangent = the tangent points.
            float2 arcStart = a + da * math.dot(center - a, da);
            float2 arcEnd   = b + db * math.dot(center - b, db);

            // Lead-in runs forward from A along da; the lead-out runs *back* from B along -db,
            // so both are measured as positive distances along the path.
            float tA = math.dot(arcStart - a, da);
            float tB = math.dot(b - arcEnd, db);
            if (tA < -1e-3f || tB < -1e-3f) return result;

            float tol = math.max(radius * 1e-3f, 0.01f);
            if (math.abs(math.distance(center, arcStart) - radius) > tol) return result;
            if (math.abs(math.distance(center, arcEnd) - radius) > tol) return result;

            float  th0      = math.atan2(arcStart.y - center.y, arcStart.x - center.x);
            float  th1      = math.atan2(arcEnd.y - center.y, arcEnd.x - center.x);
            float2 tanCcw   = new float2(-math.sin(th0), math.cos(th0));
            bool   ccw      = math.dot(tanCcw, da) > 0f;

            float sweep = th1 - th0;
            if (ccw) {
                if (sweep <= 0f) sweep += 2f * math.PI;
            } else {
                if (sweep >= 0f) sweep -= 2f * math.PI;
            }

            // Tangency is only half of it: the arc must *enter* A along da and *leave* toward B
            // along db. Without this check the mirror branch passes tangency but doubles back.
            float2 tanStart = ccw ? tanCcw : -tanCcw;
            float2 tanEnd   = ccw
                ? new float2(-math.sin(th1), math.cos(th1))
                : new float2(math.sin(th1), -math.cos(th1));

            if (math.dot(tanStart, da) < 0.999f) return result;
            if (math.dot(tanEnd, db) < 0.999f) return result;

            return new Candidate {
                Center      = center,
                Radius      = radius,
                ArcStart    = arcStart,
                ArcEnd      = arcEnd,
                StartOffset = math.max(0f, tA),
                EndOffset   = math.max(0f, tB),
                Sweep       = sweep,
                Ccw         = ccw,
                Valid       = true,
                IsShort     = math.abs(sweep) <= math.PI + 1e-4f,
            };
        }

        /// <summary>
        ///     Narrows the radius band so that <c>c0 + c1·r &gt;= 0</c> holds.
        ///     Returns false when the constraint can never be satisfied.
        /// </summary>
        private static bool ApplyNonNegative(float c0, float c1, ref float lo, ref float hi) {
            if (math.abs(c1) < 1e-6f) return c0 >= 0f;

            float bound = -c0 / c1;
            if (c1 > 0f) {
                lo = math.max(lo, bound);
            } else {
                hi = math.min(hi, bound);
            }
            return true;
        }

        // ── Handle support ──────────────────────────────────────────────────────

        /// <summary>
        ///     Resolves the circle centre for an explicit radius factor. Used by the drag handle so
        ///     it can slide along the centre axis without touching the parameter directly.
        /// </summary>
        public static bool TryGetCenter(NT_ConnectToolSystem tool, float factor, out float3 center) {
            center = default;

            var config = tool.BuildJobConfig();
            config.LoopRadiusFactor = math.saturate(factor);

            if (!TrySolve(in config, out var solution)) return false;

            if (solution.Kind == LoopKind.Stacked) {
                // The knob rides the deck: that is the part of the link whose distance from START
                // grows with the radius, and it is where the flyover actually is.
                center = new float3(solution.Center.x, DeckHeight(in config), solution.Center.y);
                return true;
            }

            ArcHeights(in config, in solution, out float arcStartY, out float arcEndY);
            center = new float3(solution.Center.x,
                                (arcStartY + arcEndY) * 0.5f,
                                solution.Center.y);
            return true;
        }

        /// <summary>
        ///     Radius of the arc the tool is currently showing, in metres, together with the point the
        ///     radius knob sits on. Feeds the in-world readout next to that knob.
        ///     <para>
        ///         Read from the <b>solved</b> geometry rather than recomputed from the factor: the
        ///         solver quantizes the radius to whole metres (and the U-turn pins it to half the node
        ///         gap), so the factor alone does not tell you what got built.
        ///     </para>
        /// </summary>
        public static bool TryGetRadius(NT_ConnectToolSystem tool, out float radius, out float3 knob) {
            radius = 0f;
            knob   = default;

            var factor = math.saturate(tool.LoopRadiusFactor.Value);
            var config = tool.BuildJobConfig();
            config.LoopRadiusFactor = factor;

            if (!TrySolve(in config, out var solution)) return false;

            radius = solution.Radius;
            return TryGetCenter(tool, factor, out knob);
        }

        /// <summary>
        ///     Returns the two ends of the drag axis: the centre at factor 0 and at factor 1.
        /// </summary>
        public static bool TryGetCenterAxis(NT_ConnectToolSystem tool, out float3 from, out float3 to) {
            bool hasFrom = TryGetCenter(tool, 0f, out from);
            bool hasTo   = TryGetCenter(tool, 1f, out to);

            if (!hasFrom || !hasTo) {
                // Degenerate geometry: collapse the axis onto the midpoint so the handle stays
                // pickable instead of jumping to the world origin.
                var config = tool.BuildJobConfig();
                var mid    = (config.StartPosition + config.EndPosition) * 0.5f;
                from = mid;
                to   = mid + new float3(1f, 0f, 0f);
                return false;
            }

            if (math.distancesq(from, to) < 0.01f) {
                // e.g. the U-turn, whose radius is pinned by the two nodes -- nothing to drag.
                to = from + new float3(1f, 0f, 0f);
                return false;
            }

            // Both ends take the height of the arc the tool is *currently* showing, so the knob
            // rides on the ribbon instead of the (possibly much lower) factor-0 plane once the
            // ramp grade lifts the loop off the ground.
            if (TryGetCenter(tool, math.saturate(tool.LoopRadiusFactor.Value), out var current)) {
                from.y = current.y;
                to.y   = current.y;
            }

            return true;
        }

        // ── Vertical profile ────────────────────────────────────────────────────

        /// <summary>
        ///     Height of the elevated deck: measured from where the <b>first loop starts</b>, not from
        ///     the nodes.
        ///     <para>
        ///         The first loop leaves START directly, so its start height <i>is</i> START's height;
        ///         adding the offset therefore makes <see cref="ConnectJobConfig.LoopHeight" /> mean
        ///         exactly "arc end minus arc start", i.e. the rise the ramp has to deliver. Taking
        ///         <c>max(startY, endY)</c> instead folded the height difference between the two nodes
        ///         into the deck, so on a pair of roads at different levels the ramps silently changed
        ///         pitch and the grade slider lost its reference.
        ///     </para>
        ///     <para>
        ///         Negative offsets are legal and put the deck in a cutting: loop 1 descends into it,
        ///         loop 2 climbs back out to END.
        ///     </para>
        /// </summary>
        private static float DeckHeight(in ConnectJobConfig config) {
            return config.StartPosition.y + config.LoopHeight;
        }

        /// <summary>
        ///     Resolves the arc's end heights for the <b>single-arc</b> shapes, i.e. the two points the
        ///     vertical profile has to hit. Derived purely from the two nodes and the design grade.
        ///     <para>
        ///         <see cref="ConnectJobConfig.LoopHeight" /> is <i>not</i> part of this: it is the
        ///         height of the flyover deck, which only exists in the stacked shape. Letting it lift
        ///         the arc here made the grade slider look broken on the other shapes -- the arc was
        ///         displaced vertically while its ends were pinned, so the ribbon read as a ramp that
        ///         started somewhere other than where the profile said it did.
        ///     </para>
        /// </summary>
        private static void ArcHeights(
            in ConnectJobConfig config,
            in LoopSolution     solution,
            out float           arcStartY,
            out float           arcEndY) {
            if (solution.Kind == LoopKind.UTurn) {
                arcStartY = config.StartPosition.y;
                arcEndY   = config.EndPosition.y;
            } else {
                ComputeProfile(in config, in solution, out arcStartY, out arcEndY);
            }
        }

        /// <summary>
        ///     Builds the vertical profile of a single-arc connection, from START to END, at the
        ///     configured design grade.
        ///     <para>
        ///         The grade is a property of the <b>entire link</b>, not of one piece of it: changing
        ///         the height by <c>deltaY</c> at <c>grade</c> needs a run of <c>|deltaY| / grade</c>
        ///         metres. That run is placed against the node the ramp should be anchored to, and the
        ///         rest of the alignment stays level &mdash; see the comment in the body for why the
        ///         anchor flips with the sign of <c>deltaY</c>.
        ///     </para>
        ///     <para>
        ///         One pitch throughout, so the curve and the straight lead-out meet tangentially in
        ///         profile as well as in plan: a descending link keeps its grade right up to END and
        ///         enters the cutting smoothly, instead of flattening inside the loop.
        ///     </para>
        ///     <para>
        ///         A run longer than the path is spread over all of it at a gentler pitch, which is
        ///         the only profile the pinned END node admits.
        ///     </para>
        /// </summary>
        private static void ComputeProfile(
            in ConnectJobConfig config,
            in LoopSolution     solution,
            out float           arcStartY,
            out float           arcEndY) {
            float startY = config.StartPosition.y;
            float endY   = config.EndPosition.y;
            float deltaY = endY - startY;

            float leadIn  = solution.StartOffset > kMinLeadIn ? solution.StartOffset : 0f;
            float leadOut = solution.EndOffset   > kMinLeadIn ? solution.EndOffset   : 0f;
            float arcLen  = solution.Radius * math.abs(solution.Sweep);
            float total   = leadIn + arcLen + leadOut;

            if (total <= 0.01f) {
                arcStartY = startY;
                arcEndY   = endY;
                return;
            }

            float sArcStart = leadIn;
            float sArcEnd   = leadIn + arcLen;

            // The grade section is |deltaY| / grade metres long, placed at one end of the link. Which
            // end depends on the direction of travel in height:
            //
            //   climbing (deltaY > 0) -- it opens at the foot of the arc. The approach stays flat and
            //       the loop is what rises, so the curve is the ramp that clears the road it crosses.
            //
            //   descending (deltaY < 0) -- it closes at END. The loop stays up at START's level and
            //       the road only dives once the curve is behind it. Hanging the descent on the arc
            //       instead (which is what "always start at the arc" did) spent the drop inside the
            //       loop and then ran dead flat from there to END: the tunnel mouth ended up under
            //       the loop, and the last stretch had no guidance at all.
            //
            // Either way there is exactly one pitch, so the arc and the lead-out hand over to each
            // other without a kink.
            float grade  = math.max(0f, config.LoopRampGrade);
            float needed = grade > 1e-5f ? math.abs(deltaY) / grade : total;
            float span   = math.min(needed, total);
            if (span < 1e-3f) {
                // No height difference: the link is flat everywhere.
                arcStartY = startY;
                arcEndY   = startY;
                return;
            }

            float rampStart = deltaY >= 0f ? sArcStart : math.max(0f, total - span);
            float rampEnd   = deltaY >= 0f ? math.min(rampStart + span, total) : total;

            arcStartY = startY + deltaY * RiseAt(sArcStart, rampStart, rampEnd);
            arcEndY   = startY + deltaY * RiseAt(sArcEnd,   rampStart, rampEnd);
        }

        /// <summary>
        ///     How much of the total rise has been covered <paramref name="s" /> metres along the link.
        ///     Flat before <paramref name="climbStart" />, rising at one pitch up to
        ///     <paramref name="climbEnd" />, flat again after it.
        /// </summary>
        private static float RiseAt(float s, float climbStart, float climbEnd) {
            float span = climbEnd - climbStart;
            if (span <= 1e-3f) return s >= climbEnd ? 1f : 0f;

            return math.saturate((s - climbStart) / span);
        }

        /// <summary>
        ///     Fraction of an arc spent climbing, when the climb has to be spent at the design grade:
        ///     <c>|dh| / grade</c> metres of a <paramref name="arcLen" /> metre arc. Returns 1 (= whole
        ///     arc climbs) when there is no grade budget left, which degrades to a uniform ramp.
        /// </summary>
        private static float ClimbFraction(float dh, float arcLen, float grade) {
            if (arcLen <= 1e-3f) return 1f;
            if (grade <= 1e-5f) return 1f;

            return math.saturate(math.abs(dh) / grade / arcLen);
        }

        /// <summary>
        ///     Fraction of the rise reached at <paramref name="t" /> along an arc. The climb occupies
        ///     the first <paramref name="climbFraction" /> of the arc (so the arc leaves the node
        ///     climbing and arrives level) or the last of it (climbing into the node).
        /// </summary>
        private static float RiseFraction(float t, float climbFraction, bool atStart) {
            float cf = math.clamp(climbFraction, 1e-4f, 1f);
            if (cf >= 0.9999f) return math.saturate(t);

            return atStart
                ? math.saturate(t / cf)
                : math.saturate((t - (1f - cf)) / cf);
        }

        // ── Node elevations ─────────────────────────────────────────────────────

        /// <summary>
        ///     Elevation a <b>created</b> node has to declare, i.e. which side of the terrain the game
        ///     should build it on: bridge above, cutting below, on the ground in between.
        ///     <para>
        ///         It has to be written on <i>every</i> node the loop creates, not just the two ends.
        ///         Leaving it at zero means "this node sits on the ground", so the game flattens that
        ///         node onto the terrain: the arc then reads as alternating high and low nodes, one per
        ///         bezier piece, instead of the ramp the profile asked for.
        ///     </para>
        ///     <para>
        ///         The comparison is against the <b>terrain under the node</b>, not against the other
        ///         node. A descent into a cutting ends below ground while both endpoints are pinned, so
        ///         any rule based on the node pair alone announces the tunnel mouth as a bridge: the
        ///         game then refuses to place it under the terrain and lifts that node back to the
        ///         surface, which tears the tail off the profile again -- the same symptom as an
        ///         undeclared node, from the opposite cause.
        ///     </para>
        /// </summary>
        private static float NodeElevation(in ConnectJobConfig config, float3 node) {
            if (!config.HasTerrain) {
                // No height data (should not happen in the job): fall back to the node pair, which
                // at least keeps the ramp a ramp.
                return SlopeUtils.ClampElevation(
                    node.y - math.min(config.StartPosition.y, config.EndPosition.y));
            }

            var   terrain   = config.Terrain;
            float clearance = node.y - TerrainUtils.SampleHeight(ref terrain, node);

            if (clearance > kGroundTolerance) return SlopeUtils.ElevatedThreshold.x;
            if (clearance < -kGroundTolerance) return SlopeUtils.TunnelThreshold.x;

            // Level with the ground: let the road follow the terrain instead of forcing a bridge or a
            // cutting over a fraction of a metre.
            return 0f;
        }

        // ── Emitting ────────────────────────────────────────────────────────────

        private static void Emit(
            in  ConnectJobConfig      config,
            in  LoopSolution          solution,
            ref NativeList<EdgeConfig> curves) {
            switch (solution.Kind) {
                case LoopKind.UTurn:
                    EmitUTurn(in config, in solution, ref curves);
                    return;
                case LoopKind.Stacked:
                    EmitStacked(in config, in solution, ref curves);
                    return;
                default:
                    EmitSingle(in config, in solution, ref curves);
                    return;
            }
        }

        /// <summary>One arc between the two nodes, with straight lead-ins along their tangents.</summary>
        private static void EmitSingle(
            in  ConnectJobConfig      config,
            in  LoopSolution          solution,
            ref NativeList<EdgeConfig> curves) {
            float3 startPos = config.StartPosition;
            float3 endPos   = config.EndPosition;

            bool hasStartStraight = solution.StartOffset > kMinLeadIn;
            bool hasEndStraight   = solution.EndOffset   > kMinLeadIn;

            // ── Vertical profile: the link climbs at the design grade, the arc rides the offset ──
            ArcHeights(in config, in solution, out float arcStartY, out float arcEndY);

            float3 arcStart = hasStartStraight
                ? new float3(solution.ArcStart.x, arcStartY, solution.ArcStart.y)
                : new float3(startPos.x, arcStartY, startPos.z);

            float3 arcEnd = hasEndStraight
                ? new float3(solution.ArcEnd.x, arcEndY, solution.ArcEnd.y)
                : new float3(endPos.x, arcEndY, endPos.z);

            // ── Lead-in ──
            if (hasStartStraight) {
                EmitStraightEdge(in config, ref curves,
                    startPos, arcStart,
                    isFirst: true, isLast: false);
            }

            // ── Arc ──
            float theta0 = math.atan2(solution.ArcStart.y - solution.Center.y,
                                      solution.ArcStart.x - solution.Center.x);

            EmitArcPiece(
                in config, ref curves,
                solution.Center, solution.Radius, theta0, solution.Sweep,
                arcStart, arcEnd,
                climbFraction: 1f, climbAtStart: true,
                isFirstSegment: !hasStartStraight, isLastSegment: !hasEndStraight);

            // ── Lead-out ──
            if (hasEndStraight) {
                EmitStraightEdge(in config, ref curves,
                    arcEnd, endPos,
                    isFirst: false, isLast: true);
            }
        }

        /// <summary>
        ///     One 180° half circle from START to END. The nodes are diametrically opposite, so the
        ///     path touches them exactly; the whole height difference is spent on the arc at the
        ///     design grade.
        /// </summary>
        private static void EmitUTurn(
            in  ConnectJobConfig      config,
            in  LoopSolution          solution,
            ref NativeList<EdgeConfig> curves) {
            float3 startPos = config.StartPosition;
            float3 endPos   = config.EndPosition;

            bool hasStartStraight = solution.StartOffset > kMinLeadIn;
            bool hasEndStraight   = solution.EndOffset   > kMinLeadIn;

            // The nodes are only exactly abeam after the fore-aft stagger has been driven out along
            // the tangents, so those runs hand over to the half circle at its own height.
            ArcHeights(in config, in solution, out float arcStartY, out float arcEndY);

            float3 arcStart = new float3(solution.ArcStart.x, arcStartY, solution.ArcStart.y);
            float3 arcEnd   = new float3(solution.ArcEnd.x,   arcEndY,   solution.ArcEnd.y);

            if (hasStartStraight) {
                EmitStraightEdge(in config, ref curves,
                    startPos, arcStart,
                    isFirst: true, isLast: false);
            }

            float arcLen      = solution.Radius * math.abs(solution.Sweep);
            float climbFactor = ClimbFraction(arcEndY - arcStartY, arcLen, config.LoopRampGrade);

            float theta0 = math.atan2(solution.ArcStart.y - solution.Center.y,
                                      solution.ArcStart.x - solution.Center.x);

            EmitArcPiece(
                in config, ref curves,
                solution.Center, solution.Radius, theta0, solution.Sweep,
                arcStart, arcEnd,
                climbFactor, climbAtStart: true,
                isFirstSegment: !hasStartStraight, isLastSegment: !hasEndStraight);

            if (hasEndStraight) {
                EmitStraightEdge(in config, ref curves,
                    arcEnd, endPos,
                    isFirst: false, isLast: true);
            }
        }

        /// <summary>
        ///     Two 270° loops joined by a level deck. Each loop is the ramp: it leaves its node and
        ///     climbs (or falls) to the deck at the design grade, arriving level so the deck runs
        ///     seamlessly into it.
        /// </summary>
        private static void EmitStacked(
            in  ConnectJobConfig      config,
            in  LoopSolution          solution,
            ref NativeList<EdgeConfig> curves) {
            float3 startPos = config.StartPosition;
            float3 endPos   = config.EndPosition;

            bool hasStartStraight = solution.StartOffset > kMinLeadIn;
            bool hasEndStraight   = solution.EndOffset   > kMinLeadIn;

            float deckY  = DeckHeight(in config);
            float radius = solution.Radius;
            float arcLen  = radius * math.abs(solution.Sweep);
            float arcLen2 = radius * math.abs(solution.Sweep2);

            float3 arcStart1 = new float3(solution.ArcStart.x, startPos.y, solution.ArcStart.y);
            float3 arcEnd1   = new float3(solution.ArcEnd.x, deckY, solution.ArcEnd.y);

            float3 deckStart = new float3(solution.ArcEnd.x, deckY, solution.ArcEnd.y);
            float3 deckEnd   = new float3(solution.DeckEnd.x, deckY, solution.DeckEnd.y);

            // The second loop lands on the abeam point of END; the stagger (if any) is a lead-out.
            float3 arcStart2 = deckEnd;
            float3 arcEnd2   = new float3(solution.ArcEnd2.x, endPos.y, solution.ArcEnd2.y);

            if (hasStartStraight) {
                EmitStraightEdge(in config, ref curves,
                    startPos, arcStart1,
                    isFirst: true, isLast: false);
            }

            // ── Loop 1: climb out of START, then level onto the deck ──
            float theta0 = math.atan2(solution.ArcStart.y - solution.Center.y,
                                      solution.ArcStart.x - solution.Center.x);

            EmitArcPiece(
                in config, ref curves,
                solution.Center, radius, theta0, solution.Sweep,
                arcStart1, arcEnd1,
                climbFraction: ClimbFraction(arcEnd1.y - arcStart1.y, arcLen, config.LoopRampGrade),
                climbAtStart: true,
                isFirstSegment: !hasStartStraight, isLastSegment: false);

            // ── Deck ──
            EmitStraightEdge(in config, ref curves,
                deckStart, deckEnd,
                isFirst: false, isLast: false);

            // ── Loop 2: level off the deck, then drop into END ──
            float theta2 = math.atan2(solution.DeckEnd.y - solution.Center2.y,
                                      solution.DeckEnd.x - solution.Center2.x);

            EmitArcPiece(
                in config, ref curves,
                solution.Center2, radius, theta2, solution.Sweep2,
                arcStart2, arcEnd2,
                climbFraction: ClimbFraction(arcEnd2.y - arcStart2.y, arcLen2, config.LoopRampGrade),
                climbAtStart: false,
                isFirstSegment: false, isLastSegment: !hasEndStraight);

            if (hasEndStraight) {
                EmitStraightEdge(in config, ref curves,
                    arcEnd2, endPos,
                    isFirst: false, isLast: true);
            }
        }

        /// <summary>
        ///     Emits one circular arc as a chain of &lt;= 90° cubic beziers, with the vertical profile
        ///     spread over them by <see cref="RiseFraction" />.
        ///     <para>
        ///         The control arms use <c>k = 4/3 · tan(Δ/4)</c> with the <b>signed</b> segment sweep,
        ///         which flips them for clockwise arcs; that factor is exact because the arm is
        ///         perpendicular to the radius at each end.
        ///     </para>
        /// </summary>
        private static void EmitArcPiece(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves,
            float2 center, float radius, float theta0, float sweep,
            float3 p0, float3 p1,
            float climbFraction, bool climbAtStart,
            bool isFirstSegment, bool isLastSegment) {
            int   segments = math.max(1, (int)math.ceil(math.abs(sweep) / (math.PI / 2f)));
            float segSweep = sweep / segments;
            float k        = (4f / 3f) * math.tan(segSweep / 4f);

            float  y0   = p0.y;
            float  y1   = p1.y;
            float3 prev = p0;

            for (int i = 0; i < segments; i++) {
                float a0 = theta0 + i * segSweep;
                float a1 = a0 + segSweep;

                float3 pa = i == 0 ? p0 : prev;
                float3 pd = i == segments - 1
                    ? p1
                    : new float3(center.x + radius * math.cos(a1), 0f, center.y + radius * math.sin(a1));

                // CCW tangent at theta is (-sin, cos); k carries the CW sign.
                var tanA = new float3(-math.sin(a0), 0f, math.cos(a0));
                var tanD = new float3(-math.sin(a1), 0f, math.cos(a1));

                float3 pb = pa + k * radius * tanA;
                float3 pc = pd - k * radius * tanD;

                float t0 = (float)i / segments;
                float t1 = (float)(i + 1) / segments;

                pa.y = math.lerp(y0, y1, RiseFraction(t0, climbFraction, climbAtStart));
                pd.y = math.lerp(y0, y1, RiseFraction(t1, climbFraction, climbAtStart));
                pb.y = math.lerp(pa.y, pd.y, 1f / 3f);
                pc.y = math.lerp(pa.y, pd.y, 2f / 3f);

                bool isFirst = isFirstSegment && i == 0;
                bool isLast  = isLastSegment  && i == segments - 1;

                var bezier = new Bezier4x3 { a = pa, b = pb, c = pc, d = pd };
                curves.Add(new EdgeConfig {
                    Bezier              = bezier,
                    Length              = MathUtils.Length(bezier),
                    StartNodeElevation  = NodeElevation(in config, pa),
                    EndNodeElevation    = NodeElevation(in config, pd),
                    StartNodeFlags      = (isFirst ? CoursePosFlags.IsFirst : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                    EndNodeFlags        = (isLast  ? CoursePosFlags.IsLast  : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                    NetPrefabEntity     = config.NetPrefabEntity,
                    NetLanePrefabEntity = config.NetLanePrefabEntity,
                });

                prev = pd;
            }
        }

        private static void EmitStraightEdge(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves,
            float3 from, float3 to,
            bool isFirst, bool isLast) {
            var dir    = to - from;
            var bezier = new Bezier4x3 {
                a = from,
                b = from + dir * (1f / 3f),
                c = from + dir * (2f / 3f),
                d = to
            };
            curves.Add(new EdgeConfig {
                Bezier              = bezier,
                Length              = MathUtils.Length(bezier),
                StartNodeElevation  = NodeElevation(in config, from),
                EndNodeElevation    = NodeElevation(in config, to),
                StartNodeFlags      = (isFirst ? CoursePosFlags.IsFirst : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                EndNodeFlags        = (isLast  ? CoursePosFlags.IsLast  : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                NetPrefabEntity     = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });
        }
    }
}
