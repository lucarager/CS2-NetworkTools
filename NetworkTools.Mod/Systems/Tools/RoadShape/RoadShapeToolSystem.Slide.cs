namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Entities;
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    using SubObject = Game.Objects.SubObject;

    /// <summary>
    ///     Partial class containing the slide of a plain node to a tunnel mouth.
    ///     The game drops a cut nearer to a node than the AddNode tool allows.
    ///     A plain node there is in the way: it joins two edges of the path and nothing else.
    ///     It is moved along the path to where the mouth belongs, and is the mouth.
    ///     The edge the mouth lies on gives the piece before it to the edge on the other side.
    ///     Both edges stay the entities they were, with everything they carry.
    ///     A node that is not plain stays, see <see cref="FindCuts" />.
    /// </summary>
    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        ///     Shortest move worth making, in meters.
        ///     A node moved to a mouth has the cover of one and is left alone from then on.
        ///     This only keeps a node from moving by a rounding error.
        /// </summary>
        private const float MinSlide = 0.1f;

        /// <summary>
        ///     Slides the plain nodes of the path that stand in the way of a mouth.
        ///     The path data then reads as if the nodes had always been there.
        /// </summary>
        /// <param name="edges">Edge states of the path, not transformed yet.</param>
        /// <param name="nodes">Node states of the path, not transformed yet.</param>
        /// <param name="context">Transform context of the path.</param>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="planned">True to slide only to mouths the apply's first pass put.</param>
        private void SlideNodes(
            ref NativeArray<EdgeState> edges,
            ref NativeArray<NodeState> nodes,
            ref ShapeTransformContext  context,
            ref TerrainHeightData      terrain,
            bool                       planned) {
            // The mouths are those of the sloped path, the pieces are cut from the path as it is.
            var sloped      = new NativeArray<EdgeState>(edges, Allocator.Temp);
            var slopedNodes = new NativeArray<NodeState>(nodes, Allocator.Temp);
            var config      = BuildJobConfig();

            ShapeTransformJob.Transform(ref sloped, ref slopedNodes, in context, in config);

            // Path positions an edge is kept between, 0 to 1 when it gives nothing away.
            var kept = new NativeArray<Bounds1>(edges.Length, Allocator.Temp);
            var slid = new NativeArray<bool>(nodes.Length, Allocator.Temp);
            var any  = false;

            for (var i = 0; i < edges.Length; i++) {
                kept[i] = new Bounds1(0f, 1f);
            }

            for (var i = 0; i < edges.Length; i++) {
                any |= FindSlides(ref terrain, sloped[i], i, edges, ref kept, ref slid, planned);
            }

            if (any) {
                var input = new NativeArray<EdgeState>(edges, Allocator.Temp);

                for (var i = 0; i < edges.Length; i++) {
                    Slide(ref edges, ref nodes, input, kept, i);
                }

                MeasurePath(ref edges, nodes, ref context);
                input.Dispose();
            }

            sloped.Dispose();
            slopedNodes.Dispose();
            kept.Dispose();
            slid.Dispose();
        }

        /// <summary>
        ///     Finds the mouths of one edge that a cut cannot make, next to a plain node.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="sloped">The edge once sloped.</param>
        /// <param name="index">Index of the edge in the path.</param>
        /// <param name="edges">Edge states of the path.</param>
        /// <param name="kept">Path positions each edge is kept between, updated.</param>
        /// <param name="slid">The nodes that move already, updated.</param>
        /// <param name="planned">True to slide only to mouths the apply's first pass put.</param>
        /// <returns>True if a node of the edge moves.</returns>
        private bool FindSlides(
            ref TerrainHeightData    terrain,
            EdgeState                sloped,
            int                      index,
            NativeArray<EdgeState>   edges,
            ref NativeArray<Bounds1> kept,
            ref NativeArray<bool>    slid,
            bool                     planned) {
            var prefab = EntityManager.GetComponentData<PrefabRef>(sloped.EdgeEntity).m_Prefab;

            if (!CanTunnel(prefab)) {
                return false;
            }

            var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
            var half     = geometry.m_DefaultWidth * 0.5f;
            var length   = MathUtils.Length(sloped.Bezier);
            var runs     = new NativeList<Bounds1>(8, Allocator.Temp);
            var any      = false;

            TunnelRuns.Split(
                ref terrain,
                sloped.Bezier,
                length,
                half,
                geometry.m_ElevationLimit,
                ref runs);

            SplitBounds(
                sloped.StartNode,
                sloped.EndNode,
                sloped.Length,
                geometry,
                out var min,
                out var max);

            // An edge too short for any cut has both limits at its middle.
            var room = min < max;

            for (var r = 1; r < runs.Length; r++) {
                var at = runs[r].min;

                // A cut goes there, or the boundary is not a mouth.
                if ((room && at >= min && at <= max)
                    || !TunnelRuns.IsMouth(
                        ref terrain,
                        sloped.Bezier,
                        at,
                        half,
                        geometry.m_ElevationLimit,
                        TunnelRuns.Tolerance)) {
                    continue;
                }

                var atStart   = room ? at < min : at < 0.5f;
                var pathStart = atStart == sloped.IsForward;
                var node      = pathStart ? index : index + 1;

                if (slid[node] || !IsPlain(edges, node)) {
                    continue;
                }

                // The mouth moves out from where the whole width has three limits, in the tunnel.
                // A node with three limits already would be its own mouth.
                // It goes out to the mouth the split found instead.
                var end    = atStart ? 0f : 1f;
                var deep   = geometry.m_ElevationLimit * 3f;
                var target = TunnelRuns.Cover(ref terrain, sloped.Bezier, end, half) >= deep
                    ? at
                    : TunnelRuns.FirstMouth(
                        ref terrain,
                        sloped.Bezier,
                        length,
                        half,
                        geometry.m_ElevationLimit,
                        end,
                        atStart ? runs[r].max : runs[r - 1].min);

                if (target < 0f || math.abs(target - end) * length < MinSlide) {
                    continue;
                }

                // Once the cuts are committed, the pieces are split on grids of their own.
                // A node only moves to a mouth the apply's first pass put, give or take the snap.
                var point = MathUtils.Position(sloped.Bezier, target).xz;

                if (planned && !IsPlanned(point, out _)) {
                    continue;
                }

                var position = sloped.IsForward ? target : 1f - target;
                var bounds   = kept[index];

                if (pathStart) {
                    bounds.min = position;
                } else {
                    bounds.max = position;
                }

                // What is left of the edge must still be one the game would make.
                if ((bounds.max - bounds.min) * length < geometry.m_DefaultWidth) {
                    continue;
                }

                kept[index] = bounds;
                slid[node]  = true;
                any         = true;
            }

            runs.Dispose();

            return any;
        }

        /// <summary>
        ///     Checks whether a node of the path joins two edges of the path and nothing else.
        ///     Both edges are of one network, and carry no object placed on them.
        ///     Such an object keeps its curve position, and would move with the curve.
        /// </summary>
        /// <param name="edges">Edge states of the path.</param>
        /// <param name="node">Index of the node in the path.</param>
        /// <returns>True if the node can move along the path.</returns>
        private bool IsPlain(NativeArray<EdgeState> edges, int node) {
            if (node <= 0 || node >= edges.Length) {
                return false;
            }

            var before   = edges[node - 1];
            var after    = edges[node];
            var entity   = before.IsForward ? before.EndNode : before.StartNode;
            var prefab   = EntityManager.GetComponentData<PrefabRef>(before.EdgeEntity).m_Prefab;
            var other    = EntityManager.GetComponentData<PrefabRef>(after.EdgeEntity).m_Prefab;
            var attached = HasAttached(before.EdgeEntity) || HasAttached(after.EdgeEntity);

            return EntityManager.GetBuffer<ConnectedEdge>(entity, true).Length == 2
                   && prefab == other
                   && !attached;
        }

        /// <summary>
        ///     Checks whether an object is placed on an edge, as a stop is.
        /// </summary>
        /// <param name="edge">The edge to check.</param>
        /// <returns>True if one of the edge's objects is attached to it.</returns>
        private bool HasAttached(Entity edge) {
            if (!EntityManager.TryGetBuffer<SubObject>(edge, true, out var objects)) {
                return false;
            }

            for (var i = 0; i < objects.Length; i++) {
                if (EntityManager.HasComponent<Attached>(objects[i].m_SubObject)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Emits a split at a point of the path, on the edge that holds it for now.
        ///     That is the given edge, or the one next to it that gives the piece away.
        /// </summary>
        /// <param name="ecb">Command buffer to emit the split with.</param>
        /// <param name="entity">The edge the point is on once the nodes have moved.</param>
        /// <param name="point">The point to cut at.</param>
        private void EmitCutNear(ref EntityCommandBuffer ecb, Entity entity, float3 point) {
            var edge    = EntityManager.GetComponentData<Edge>(entity);
            var best    = entity;
            var bestAt  = 0f;
            var bestGap = float.MaxValue;
            var near    = new NativeList<Entity>(4, Allocator.Temp);
            var atStart = EntityManager.GetBuffer<ConnectedEdge>(edge.m_Start, true);
            var atEnd   = EntityManager.GetBuffer<ConnectedEdge>(edge.m_End, true);

            for (var i = 0; i < atStart.Length; i++) {
                near.Add(atStart[i].m_Edge);
            }

            for (var i = 0; i < atEnd.Length; i++) {
                near.Add(atEnd[i].m_Edge);
            }

            for (var i = 0; i < near.Length; i++) {
                var bezier = EntityManager.GetComponentData<Curve>(near[i]).m_Bezier;
                var gap    = MathUtils.Distance(bezier.xz, point.xz, out var at);

                if (gap < bestGap) {
                    best    = near[i];
                    bestAt  = at;
                    bestGap = gap;
                }
            }

            near.Dispose();

            var prefab = EntityManager.GetComponentData<PrefabRef>(best).m_Prefab;
            var curve  = EntityManager.GetComponentData<Curve>(best).m_Bezier;

            EntityManager.TryGetComponent<PseudoRandomSeed>(best, out var seed);
            NetCourseEmitter.EmitSplit(
                ref ecb,
                prefab,
                seed.m_Seed,
                MathUtils.Position(curve, bestAt),
                bestAt);
        }

        /// <summary>
        ///     Gives one edge the curve it has once the nodes have moved.
        ///     It keeps its own part, and takes what the edges before and after it give away.
        /// </summary>
        /// <param name="edges">Edge states of the path, updated.</param>
        /// <param name="nodes">Node states of the path, updated.</param>
        /// <param name="input">Edge states of the path as they were.</param>
        /// <param name="kept">Path positions each edge is kept between.</param>
        /// <param name="index">Index of the edge in the path.</param>
        private static void Slide(
            ref NativeArray<EdgeState> edges,
            ref NativeArray<NodeState> nodes,
            NativeArray<EdgeState>     input,
            NativeArray<Bounds1>       kept,
            int                        index) {
            var before = index > 0 && kept[index - 1].max < 1f;
            var after  = index < input.Length - 1 && kept[index + 1].min > 0f;
            var whole  = kept[index].min <= 0f && kept[index].max >= 1f;

            if (!before && !after && whole) {
                return;
            }

            var curve = MathUtils.Cut(Along(input[index]), kept[index]);

            if (before || after) {
                var first = before
                    ? MathUtils.Cut(Along(input[index - 1]), new Bounds1(kept[index - 1].max, 1f))
                    : curve;
                var last  = after
                    ? MathUtils.Cut(Along(input[index + 1]), new Bounds1(0f, kept[index + 1].min))
                    : curve;

                curve = Join(first, curve, last, before, after);
            }

            var edge = edges[index];

            edge.Bezier          = edge.IsForward ? curve : MathUtils.Invert(curve);
            edge.Length          = MathUtils.Length(edge.Bezier);
            edge.OriginalBezierA = edge.Bezier.a;
            edge.OriginalBezierD = edge.Bezier.d;
            edge.CalculateControlPointRatios();
            edges[index] = edge;

            // The node at the start of the edge is where the edge starts now.
            if (index > 0 && (before || kept[index].min > 0f)) {
                var node = nodes[index];

                node.Position         = curve.a;
                node.OriginalPosition = curve.a;
                nodes[index]          = node;
            }
        }

        /// <summary>
        ///     Gets the curve of an edge in the direction of the path.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <returns>The curve, from the edge's path start to its path end.</returns>
        private static Bezier4x3 Along(EdgeState edge) {
            return edge.IsForward ? edge.Bezier : MathUtils.Invert(edge.Bezier);
        }

        /// <summary>
        ///     Joins a curve with the short pieces it takes at its ends.
        ///     A cubic goes on past its ends as the same curve, so the edge stays where it was.
        ///     Its new ends are then put on the ends of the pieces, which are a step away at most.
        /// </summary>
        /// <param name="first">The piece before the curve, or the curve itself.</param>
        /// <param name="curve">The curve.</param>
        /// <param name="last">The piece after the curve, or the curve itself.</param>
        /// <param name="before">True if there is a piece before the curve.</param>
        /// <param name="after">True if there is a piece after the curve.</param>
        /// <returns>One curve over the three.</returns>
        private static Bezier4x3 Join(
            Bezier4x3 first,
            Bezier4x3 curve,
            Bezier4x3 last,
            bool      before,
            bool      after) {
            var lead   = before ? MathUtils.Length(first) / Speed(curve, curve.b - curve.a) : 0f;
            var trail  = after ? MathUtils.Length(last) / Speed(curve, curve.d - curve.c) : 0f;
            var result = MathUtils.Cut(curve, new Bounds1(-lead, 1f + trail));

            if (before) {
                var shift = first.a - result.a;

                result.a += shift;
                result.b += shift;
            }

            if (after) {
                var shift = last.d - result.d;

                result.c += shift;
                result.d += shift;
            }

            return result;
        }

        /// <summary>
        ///     Gets the length a curve covers per unit of curve position at one of its ends.
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="handle">From the end to its control point, or the reverse.</param>
        /// <returns>The speed, the whole curve's when the control point is on the end.</returns>
        private static float Speed(Bezier4x3 curve, float3 handle) {
            var speed = 3f * math.length(handle);

            return speed > 1f ? speed : math.max(MathUtils.Length(curve), 1f);
        }

        /// <summary>
        ///     Measures the path again once its edges have changed.
        ///     That is every distance and ratio, and the total length.
        ///     The same figures as <see cref="GatherPathDataJob" /> gives a path.
        /// </summary>
        /// <param name="edges">Edge states of the path, updated.</param>
        /// <param name="nodes">Node states of the path.</param>
        /// <param name="context">Transform context of the path, updated.</param>
        private static void MeasurePath(
            ref NativeArray<EdgeState> edges,
            NativeArray<NodeState>     nodes,
            ref ShapeTransformContext  context) {
            var distance = 0f;

            for (var i = 0; i < edges.Length; i++) {
                var edge  = edges[i];
                var curve = Along(edge);

                if (i > 0) {
                    distance += math.distance(Along(edges[i - 1]).d, nodes[i].OriginalPosition);
                }

                distance += math.distance(nodes[i].OriginalPosition, curve.a);

                edge.CumulativeDistance = distance;
                edges[i]                = edge;
                distance               += edge.Length;
            }

            distance += math.distance(
                Along(edges[edges.Length - 1]).d,
                nodes[edges.Length].OriginalPosition);

            context.TotalLength = distance;

            for (var i = 0; i < edges.Length; i++) {
                var edge  = edges[i];
                var start = edge.CumulativeDistance;

                edge.StartPointAbsoluteRatio = start / distance;
                edge.EndPointAbsoluteRatio   = (start + edge.Length) / distance;
                edge.StartControlPointAbsoluteRatio =
                    (start + edge.StartControlPointRatio * edge.Length) / distance;
                edge.EndControlPointAbsoluteRatio =
                    (start + edge.EndControlPointRatio * edge.Length) / distance;
                edges[i] = edge;
            }
        }
    }
}
