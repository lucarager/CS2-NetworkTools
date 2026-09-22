namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Entities;
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine.Rendering;

    /// <summary>
    ///     Partial class containing the tunnel mode of the slope templates.
    ///     The slope is written in place, which keeps everything the edges carry.
    ///     Writing in place cannot add the nodes a tunnel must start and end at.
    ///     So the apply takes three frames.
    ///     First the path is cut where <see cref="TunnelRuns.Split" /> cuts the sloped curves.
    ///     The game itself makes the cuts, the way the AddNode tool does it.
    ///     Then the cuts are committed.
    ///     A mouth the commit left without a node is made and committed the same way, once.
    ///     Last, the slope is applied to the path found again.
    ///     The path takes the elevations of a network laid there.
    ///     <see cref="NT_TunnelMouthSystem" /> corrects them as it does for the Connect tool.
    ///     The ground is the map's own heightmap all along.
    ///     The heights the game hands out are shaped by the path where it lies now.
    ///     A road joining the path at a node the slope moves is a <see cref="Branch" />.
    ///     A branch is cut and given its elevations the same way.
    ///     A plain node in the way of a mouth is moved to it, see <see cref="SlideNodes" />.
    ///     The preview shows the tunnels beforehand, see <see cref="EmitTunnelPreview" />.
    /// </summary>
    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        ///     Distance from a cut within which the game puts its node.
        ///     The game moves a split to the nearest multiple of 4 m along the edge it cuts.
        /// </summary>
        private const float SnapReach = 2f + TunnelRuns.Tolerance;

        /// <summary>
        ///     The first edge of a road joining the path, seen from the node they share.
        /// </summary>
        private struct Branch {
            /// <summary>
            ///     The path node the road joins.
            /// </summary>
            public Entity Junction;

            /// <summary>
            ///     The curve the apply gives the edge, starting at the junction.
            ///     Its end and control point at the node are moved with the node.
            /// </summary>
            public Bezier4x3 Sloped;
        }

        /// <summary>
        ///     An edge the tunnel preview lays again, with the elevations it has now.
        ///     The pieces of the preview take from it what the apply would keep.
        /// </summary>
        private struct Previewed {
            /// <summary>
            ///     The curve of the edge once sloped.
            /// </summary>
            public Bezier4x3 Sloped;

            /// <summary>
            ///     The elevation of the edge's start node.
            /// </summary>
            public float2 Start;

            /// <summary>
            ///     The elevation of the edge.
            /// </summary>
            public float2 Edge;

            /// <summary>
            ///     The elevation of the edge's end node.
            /// </summary>
            public float2 End;

            /// <summary>
            ///     True for an edge of the path, false for a road joining it.
            /// </summary>
            public bool OnPath;
        }

        /// <summary>
        ///     Stage of the tunnel apply.
        ///     0: not started.
        ///     1: the cuts are previewed, commit them.
        ///     2: the cuts are committed, cut what is missing or apply the slope.
        /// </summary>
        private int m_TunnelStage;

        /// <summary>
        ///     True once the apply has cut a second time, for the cuts the first commit left out.
        /// </summary>
        private bool m_CutAgain;

        /// <summary>
        ///     The map's own heights, read back for a selected path, see <see cref="MapTerrain" />.
        /// </summary>
        private NativeArray<ushort> m_MapHeights;

        private NativeList<Branch>    m_Branches;
        private NativeList<Previewed> m_Previewed;

        /// <summary>
        ///     Edges the tunnel preview lays again in place, see <see cref="PreviewsInPlace" />.
        /// </summary>
        private NativeHashSet<Entity> m_InPlace;

        /// <summary>
        ///     Where the apply's first pass put the mouths, in x and z.
        ///     They are its cuts and the nodes it slid.
        ///     A piece split alone after the commit is sampled on a grid of its own.
        ///     It may find a mouth that neither the split of the whole edge nor the preview had.
        ///     So from the commit on, the apply cuts and slides to these alone.
        /// </summary>
        private NativeList<float2> m_Mouths;

        /// <summary>
        ///     Gets whether the current template is one of the slope templates.
        /// </summary>
        private bool IsSlope {
            get {
                return Template.Value == ShapeTransformTemplate.SlopeLinear
                       || Template.Value == ShapeTransformTemplate.SlopeEaseInOut
                       || Template.Value == ShapeTransformTemplate.SlopeArch;
            }
        }

        /// <summary>
        ///     Gets whether the temporary entities are a preview of tunnels.
        ///     Those of an apply under way are its cuts.
        /// </summary>
        internal bool PreviewsTunnels {
            get {
                return Tunnel.Value && IsSlope && m_TunnelStage == 0;
            }
        }

        /// <summary>
        ///     Runs one frame of the tunnel apply, see the class summary for the stages.
        /// </summary>
        /// <param name="inputDeps">Input job dependencies.</param>
        /// <returns>The job handle of the frame's work.</returns>
        private JobHandle ApplyTunnel(JobHandle inputDeps) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            switch (m_TunnelStage) {
                case 0 when EmitCuts(false):
                    applyMode     = ApplyMode.Clear;
                    m_TunnelStage = 1;

                    return inputDeps;
                case 1:
                    applyMode     = ApplyMode.Apply;
                    m_TunnelStage = 2;

                    return inputDeps;
                case 2:
                    // A selected node the commit merged away leaves no path to slope.
                    if (!FindPathAgain()) {
                        DropTunnelApply();
                        applyMode = ApplyMode.Clear;
                        ResetToIdle();

                        return inputDeps;
                    }

                    RefreshPathData();

                    // The commit merges away a plain node between two edges of one kind.
                    // A mouth that node was to slide to has no node then: it is cut in turn.
                    if (!m_CutAgain && EmitCuts(true)) {
                        applyMode     = ApplyMode.Clear;
                        m_TunnelStage = 1;
                        m_CutAgain    = true;

                        return inputDeps;
                    }

                    break;
            }

            applyMode     = ApplyMode.Clear;
            m_TunnelStage = 0;
            m_CutAgain    = false;

            var terrain     = MapTerrain();
            var mouthSystem = World.GetOrCreateSystemManaged<NT_TunnelMouthSystem>();

            if (m_PathDataValid && m_EdgeStates.Length > 0) {
                var pathEdges = m_EdgeStates.AsArray();
                var pathNodes = m_NodeStates.AsArray();

                SlideNodes(
                    ref pathEdges,
                    ref pathNodes,
                    ref m_ShapeTransformContext,
                    ref terrain,
                    true);
            }

            mouthSystem.Rewritten(m_CurrentPathEdges.AsArray(), terrain);

            var jobHandle = SchedulePathTransformJob(inputDeps, ToolOutputMode.Apply, terrain);

            // Its command buffer plays back after the job's, so its curves win on the branches.
            SlopeBranches(terrain);
            m_Mouths.Clear();
            ResetToIdle();

            return jobHandle;
        }

        /// <summary>
        ///     Drops an apply cut short, by a change of tool or by a path the commit broke.
        /// </summary>
        private void DropTunnelApply() {
            m_TunnelStage = 0;
            m_CutAgain    = false;
            m_Branches.Clear();
            m_Mouths.Clear();
        }

        /// <summary>
        ///     Reads the map's heightmap back from the GPU, as the game does to save it.
        ///     It stands in for the heights the game hands out, which have the networks dug in.
        /// </summary>
        /// <returns>Terrain height data over the map's own heights.</returns>
        internal TerrainHeightData MapTerrain() {
            var shaped = m_TerrainSystem.GetHeightData();

            // The readback blocks: once for a selected path, not at every preview.
            if (!m_MapHeights.IsCreated) {
                m_MapHeights = new NativeArray<ushort>(shaped.heights.Length, Allocator.Persistent);

                AsyncGPUReadback.RequestIntoNativeArray(ref m_MapHeights, m_TerrainSystem.heightmap)
                                .WaitForCompletion();
            }

            return new TerrainHeightData(
                m_MapHeights,
                default,
                shaped.resolution,
                shaped.scale,
                shaped.offset,
                false);
        }

        /// <summary>
        ///     Frees the copy of the map's heightmap, on the first update without a selected path.
        ///     A tool left with a path selected frees it as it stops.
        ///     The slope job and the mouth system read it until the end of the apply's last frame.
        /// </summary>
        private void ReleaseMapHeights() {
            if (m_MapHeights.IsCreated) {
                m_MapHeights.Dispose();
            }
        }

        /// <summary>
        ///     Emits a split for each cut inside an edge of the path or of a branch.
        /// </summary>
        /// <param name="missing">True to emit only the cuts that have no node yet.</param>
        /// <returns>True if at least one split was emitted.</returns>
        private bool EmitCuts(bool missing) {
            // A branch is recorded whole, before the commit cuts it into pieces.
            // SlopeBranches finds those pieces again along the curve kept here.
            if (!missing) {
                m_Branches.Clear();
                m_Mouths.Clear();
            }

            if (!m_PathDataValid || m_EdgeStates.Length == 0) {
                return false;
            }

            var edges   = new NativeArray<EdgeState>(m_EdgeStates.AsArray(), Allocator.Temp);
            var nodes   = new NativeArray<NodeState>(m_NodeStates.AsArray(), Allocator.Temp);
            var config  = BuildJobConfig();
            var context = m_ShapeTransformContext;
            var terrain = MapTerrain();

            SlideNodes(ref edges, ref nodes, ref context, ref terrain, missing);

            // A node slid to a mouth is one of the mouths the commit is checked against.
            if (!missing) {
                for (var i = 0; i < nodes.Length; i++) {
                    var from = m_NodeStates[i].Position.xz;

                    if (math.lengthsq(nodes[i].Position.xz - from) > 1e-6f) {
                        m_Mouths.Add(nodes[i].Position.xz);
                    }
                }
            }

            ShapeTransformJob.Transform(ref edges, ref nodes, in context, in config);

            var ecb = m_Barrier.CreateCommandBuffer();
            var any = false;

            for (var i = 0; i < edges.Length; i++) {
                var state = edges[i];

                any |= EmitCuts(
                    ref ecb,
                    ref terrain,
                    state.EdgeEntity,
                    state.Bezier,
                    state.Length,
                    missing);
            }

            var moved = new NativeHashMap<Entity, float3>(nodes.Length, Allocator.Temp);
            var done  = new NativeHashSet<Entity>(nodes.Length, Allocator.Temp);

            for (var i = 0; i < nodes.Length; i++) {
                var delta = nodes[i].Position - nodes[i].OriginalPosition;

                if (math.lengthsq(delta) >= 1e-6f) {
                    moved.TryAdd(nodes[i].Entity, delta);
                }
            }

            // The roads joining the path at a node the slope moves.
            for (var i = 0; i < nodes.Length; i++) {
                var node = nodes[i].Entity;

                if (!moved.ContainsKey(node)) {
                    continue;
                }

                var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true);

                for (var j = 0; j < connected.Length; j++) {
                    var entity = connected[j].m_Edge;
                    var edge   = EntityManager.GetComponentData<Edge>(entity);
                    var inPath = m_CurrentPathEdges.Contains(entity);
                    var joins  = edge.m_Start == node || edge.m_End == node;

                    if (inPath || !joins || !done.Add(entity)) {
                        continue;
                    }

                    var bezier = EntityManager.GetComponentData<Curve>(entity).m_Bezier;
                    var length = EntityManager.GetComponentData<Curve>(entity).m_Length;

                    // An edge joining the path at both ends follows both nodes.
                    if (moved.TryGetValue(edge.m_Start, out var start)) {
                        bezier.a += start;
                        bezier.b += start;
                    }

                    if (moved.TryGetValue(edge.m_End, out var end)) {
                        bezier.c += end;
                        bezier.d += end;
                    }

                    any |= EmitCuts(ref ecb, ref terrain, entity, bezier, length, missing);

                    if (!missing) {
                        m_Branches.Add(new Branch {
                            Junction = node,
                            Sloped   = edge.m_Start == node ? bezier : MathUtils.Invert(bezier)
                        });
                    }
                }
            }

            edges.Dispose();
            nodes.Dispose();
            moved.Dispose();
            done.Dispose();

            return any;
        }

        /// <summary>
        ///     Checks whether Tunnel mode applies to an edge of this network prefab.
        ///     See <see cref="TunnelRuns.CanTunnel" />.
        /// </summary>
        /// <param name="prefab">The network prefab of the edge.</param>
        /// <returns>True if the edge is cut and given elevations.</returns>
        private bool CanTunnel(Entity prefab) {
            if (!EntityManager.HasComponent<NetGeometryData>(prefab)
                || !EntityManager.HasComponent<PlaceableNetData>(prefab)) {
                return false;
            }

            return TunnelRuns.CanTunnel(EntityManager.GetComponentData<PlaceableNetData>(prefab));
        }

        /// <summary>
        ///     Emits the splits of one edge, for the curve the apply will give it.
        /// </summary>
        /// <param name="ecb">Command buffer to emit the splits with.</param>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="entity">The edge to cut.</param>
        /// <param name="sloped">The curve of the edge once sloped.</param>
        /// <param name="length">Length of the edge before the slope.</param>
        /// <param name="missing">True to emit only the cuts that have no node yet.</param>
        /// <returns>True if at least one split was emitted.</returns>
        private bool EmitCuts(
            ref EntityCommandBuffer ecb,
            ref TerrainHeightData   terrain,
            Entity                  entity,
            Bezier4x3               sloped,
            float                   length,
            bool                    missing) {
            var prefab = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;

            if (!CanTunnel(prefab)) {
                return false;
            }

            var curve = EntityManager.GetComponentData<Curve>(entity);
            var cuts  = new NativeList<float>(8, Allocator.Temp);

            EntityManager.TryGetComponent<PseudoRandomSeed>(entity, out var seed);

            if (missing) {
                FindPlanned(entity, sloped, length, ref cuts);
            } else {
                FindCuts(ref terrain, entity, sloped, length, ref cuts);
            }

            // An edge whose node slides is cut where its new curve lies now, on it or next to it.
            var slid = math.any(sloped.a.xz != curve.m_Bezier.a.xz)
                       || math.any(sloped.d.xz != curve.m_Bezier.d.xz);

            for (var i = 0; i < cuts.Length; i++) {
                var point = MathUtils.Position(sloped, cuts[i]);

                if (!missing) {
                    m_Mouths.Add(point.xz);
                }

                if (slid) {
                    EmitCutNear(ref ecb, entity, point);

                    continue;
                }

                var position = MathUtils.Position(curve.m_Bezier, cuts[i]);

                NetCourseEmitter.EmitSplit(ref ecb, prefab, seed.m_Seed, position, cuts[i]);
            }

            var any = cuts.Length > 0;

            cuts.Dispose();

            return any;
        }

        /// <summary>
        ///     Finds where an edge is cut, for the curve the apply will give it.
        ///     The preview cuts its pieces at the same places.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="entity">The edge to cut, of a network that can tunnel.</param>
        /// <param name="sloped">The curve of the edge once sloped.</param>
        /// <param name="length">Length of the edge before the slope.</param>
        /// <param name="cuts">Output: the curve positions of the cuts, in order.</param>
        private void FindCuts(
            ref TerrainHeightData terrain,
            Entity                entity,
            Bezier4x3             sloped,
            float                 length,
            ref NativeList<float> cuts) {
            var prefab   = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
            var runs     = new NativeList<Bounds1>(8, Allocator.Temp);

            TunnelRuns.Split(
                ref terrain,
                sloped,
                MathUtils.Length(sloped),
                geometry.m_DefaultWidth * 0.5f,
                geometry.m_ElevationLimit,
                ref runs);

            var edge = EntityManager.GetComponentData<Edge>(entity);

            SplitBounds(edge.m_Start, edge.m_End, length, geometry, out var min, out var max);

            var minDistance = TunnelRuns.MinPortalSamples * TunnelRuns.Step;
            var halfWidth   = geometry.m_DefaultWidth * 0.5f;
            var nearMouth   = TunnelRuns.NearMouthDepth(geometry.m_ElevationLimit);
            var previous    = 0f;

            for (var r = 1; r < runs.Length && min < max; r++) {
                var t = math.clamp(runs[r].min, min, max);

                // A mouth the clamp would move goes to the end node that has nearly the cover.
                // Clamped, it would leave a slot between that node and the head wall.
                var moved = runs[r].min < min || runs[r].min > max;
                var node  = runs[r].min < min ? 0f : 1f;

                if (moved
                    && TunnelRuns.Cover(ref terrain, sloped, runs[r].min, halfWidth) >= nearMouth
                    && TunnelRuns.Cover(ref terrain, sloped, node, halfWidth) >= nearMouth) {
                    continue;
                }

                if (previous > 0f && (t - previous) * length < minDistance) {
                    continue;
                }

                cuts.Add(t);
                previous = t;
            }

            runs.Dispose();
        }

        /// <summary>
        ///     Finds the mouths of the first pass that the commit left without a node.
        ///     The game moves a cut by half a step at most: a cut that near a node has its node.
        ///     Any cut the clamp would move is left out.
        /// </summary>
        /// <param name="entity">The edge to cut, of a network that can tunnel.</param>
        /// <param name="sloped">The curve of the edge once sloped.</param>
        /// <param name="length">Length of the edge before the slope.</param>
        /// <param name="cuts">Output: the curve positions of the cuts, in order.</param>
        private void FindPlanned(
            Entity                entity,
            Bezier4x3             sloped,
            float                 length,
            ref NativeList<float> cuts) {
            var prefab   = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
            var edge     = EntityManager.GetComponentData<Edge>(entity);

            SplitBounds(edge.m_Start, edge.m_End, length, geometry, out var min, out var max);

            for (var i = 0; i < m_Mouths.Length; i++) {
                var gap = MathUtils.Distance(sloped.xz, m_Mouths[i], out var t);

                if (gap < 1f && t > min && t < max) {
                    cuts.Add(t);
                }
            }

            cuts.Sort();
        }

        /// <summary>
        ///     Gets the curve positions an edge may be cut between.
        ///     A cut too near an end is merged with its node, as the AddNode tool finds.
        ///     An end joined to other edges keeps cuts further away.
        /// </summary>
        /// <param name="start">The start node of the edge.</param>
        /// <param name="end">The end node of the edge.</param>
        /// <param name="length">Length of the edge.</param>
        /// <param name="geometry">The geometry data of the edge's prefab.</param>
        /// <param name="min">Output: the least curve position of a cut.</param>
        /// <param name="max">Output: the greatest.</param>
        private void SplitBounds(
            Entity          start,
            Entity          end,
            float           length,
            NetGeometryData geometry,
            out float       min,
            out float       max) {
            NT_EdgeUtils.GetMinMaxSplitPositions(
                length,
                geometry.m_DefaultWidth,
                geometry.m_EdgeLengthRange.min,
                EntityManager.GetBuffer<ConnectedEdge>(start, true).Length > 1,
                EntityManager.GetBuffer<ConnectedEdge>(end, true).Length > 1,
                out min,
                out max);
        }

        /// <summary>
        ///     Checks whether the apply's first pass put a mouth within <see cref="SnapReach" />.
        /// </summary>
        /// <param name="point">The mouth, in x and z.</param>
        /// <param name="mouth">Output: the mouth the first pass put there.</param>
        /// <returns>True if the first pass put a mouth that near.</returns>
        private bool IsPlanned(float2 point, out float2 mouth) {
            for (var i = 0; i < m_Mouths.Length; i++) {
                if (math.distance(m_Mouths[i], point) <= SnapReach) {
                    mouth = m_Mouths[i];

                    return true;
                }
            }

            mouth = default;

            return false;
        }

        /// <summary>
        ///     Previews the sloped path as the apply will leave it, tunnels and mouths included.
        ///     The preview is never committed, so it is free to be another network.
        ///     The path and the roads joining it at a node the slope moves are hidden.
        ///     Their sloped curves are laid as new courses, cut where the apply cuts them.
        ///     An edge that keeps its two nodes and has no cut is recreated in place instead.
        ///     The game then builds the preview as it does one of the Connect tool.
        /// </summary>
        private void EmitTunnelPreview() {
            m_Previewed.Clear();
            m_InPlace.Clear();

            if (!m_PathDataValid || m_EdgeStates.Length == 0) {
                return;
            }

            var edges   = new NativeArray<EdgeState>(m_EdgeStates.AsArray(), Allocator.Temp);
            var nodes   = new NativeArray<NodeState>(m_NodeStates.AsArray(), Allocator.Temp);
            var moved   = new NativeHashMap<Entity, float3>(nodes.Length, Allocator.Temp);
            var done    = new NativeHashSet<Entity>(nodes.Length, Allocator.Temp);
            var config  = BuildJobConfig();
            var context = m_ShapeTransformContext;
            var terrain = MapTerrain();

            SlideNodes(ref edges, ref nodes, ref context, ref terrain, false);
            ShapeTransformJob.Transform(ref edges, ref nodes, in context, in config);

            // A node moves with the slope, or along the path to a mouth.
            for (var i = 0; i < nodes.Length; i++) {
                var position = EntityManager.GetComponentData<Node>(nodes[i].Entity).m_Position;

                if (math.lengthsq(nodes[i].Position - position) >= 1e-6f) {
                    moved.TryAdd(nodes[i].Entity, nodes[i].Position);
                }
            }

            var ecb = m_Barrier.CreateCommandBuffer();

            for (var i = 0; i < edges.Length; i++) {
                var state = edges[i];

                EmitPieces(
                    ref ecb,
                    ref terrain,
                    state.EdgeEntity,
                    state.Bezier,
                    state.Length,
                    moved,
                    true);
            }

            // The game leaves a moved node alone only once all its edges are deleted.
            for (var i = 0; i < nodes.Length; i++) {
                var node = nodes[i].Entity;

                if (!moved.ContainsKey(node)) {
                    continue;
                }

                var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true)
                                             .ToNativeArray(Allocator.Temp);

                for (var j = 0; j < connected.Length; j++) {
                    var entity = connected[j].m_Edge;
                    var edge   = EntityManager.GetComponentData<Edge>(entity);
                    var inPath = m_CurrentPathEdges.Contains(entity);
                    var joins  = edge.m_Start == node || edge.m_End == node;

                    if (inPath || !joins || !done.Add(entity)) {
                        continue;
                    }

                    // The control point at a moved node follows it, which keeps the tangent there.
                    var bezier = EntityManager.GetComponentData<Curve>(entity).m_Bezier;

                    if (moved.TryGetValue(edge.m_Start, out var start)) {
                        bezier.b += start - bezier.a;
                    }

                    if (moved.TryGetValue(edge.m_End, out var end)) {
                        bezier.c += end - bezier.d;
                    }

                    EmitPieces(
                        ref ecb,
                        ref terrain,
                        entity,
                        bezier,
                        EntityManager.GetComponentData<Curve>(entity).m_Length,
                        moved,
                        false);
                }

                connected.Dispose();
            }

            edges.Dispose();
            nodes.Dispose();
            moved.Dispose();
            done.Dispose();
        }

        /// <summary>
        ///     Hides one edge and lays its sloped curve as new courses, one per piece.
        ///     An edge that keeps its two nodes and has no cut is recreated in place instead.
        ///     An end at a node the slope moves is a new node, found by its position.
        /// </summary>
        /// <param name="ecb">Command buffer to emit the definitions with.</param>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="entity">The edge to preview.</param>
        /// <param name="sloped">The curve of the edge once sloped.</param>
        /// <param name="length">Length of the edge before the slope.</param>
        /// <param name="moved">The nodes the slope moves, with their new positions.</param>
        /// <param name="onPath">True for an edge of the path, false for a road joining it.</param>
        private void EmitPieces(
            ref EntityCommandBuffer       ecb,
            ref TerrainHeightData         terrain,
            Entity                        entity,
            Bezier4x3                     sloped,
            float                         length,
            NativeHashMap<Entity, float3> moved,
            bool                          onPath) {
            var edge     = EntityManager.GetComponentData<Edge>(entity);
            var curve    = EntityManager.GetComponentData<Curve>(entity);
            var prefab   = EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);

            EntityManager.TryGetComponent<PseudoRandomSeed>(entity, out var seed);
            EntityManager.TryGetComponent<Upgraded>(entity, out var upgraded);

            // A node that stays is the node it was, a moved one is found by its new position.
            // The game takes two ends for one node when their positions are the same exactly.
            var startNode = edge.m_Start;
            var endNode   = edge.m_End;

            if (moved.TryGetValue(edge.m_Start, out var start)) {
                startNode = Entity.Null;
                sloped.a  = start;
            }

            if (moved.TryGetValue(edge.m_End, out var end)) {
                endNode  = Entity.Null;
                sloped.d = end;
            }

            var cuts = new NativeList<float>(8, Allocator.Temp);

            if (CanTunnel(prefab)) {
                FindCuts(ref terrain, entity, sloped, length, ref cuts);
            }

            // The game drops a new course between two nodes that an edge joins already.
            // Such an edge is previewed in place, as without Tunnel mode.
            var inPlace  = startNode != Entity.Null && endNode != Entity.Null && cuts.Length == 0;
            var existing = inPlace ? sloped : curve.m_Bezier;
            var original = new EdgeConfig {
                StartNodeEntity   = edge.m_Start,
                EndNodeEntity     = edge.m_End,
                StartNodePosition = existing.a,
                EndNodePosition   = existing.d,
                StartNodeRotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(existing)),
                EndNodeRotation   = NetUtils.GetNodeRotation(MathUtils.EndTangent(existing)),
                Bezier            = existing,
                Length            = MathUtils.Length(existing)
            };

            if (inPlace) {
                // Upstream's preview gives its courses these flags.
                original.StartNodeFlags = CoursePosFlags.FreeHeight
                                          | CoursePosFlags.IsGrid
                                          | CoursePosFlags.IsRight;
                original.EndNodeFlags   = original.StartNodeFlags;

                // Upstream's preview names no node: the game then leaves the nodes where they are.
                // A node it knows is one it moves or merges away between two edges in line.
                original.StartNodeEntity = Entity.Null;
                original.EndNodeEntity   = Entity.Null;
                original.CourseElevation = ElevationOf(entity);
                original.NetPrefabEntity = prefab;
                original.RandomSeed      = seed.m_Seed;

                // Upstream's preview flags the path as a parent, which the game outlines in green.
                var flags = onPath
                    ? CreationFlags.Recreate | CreationFlags.Parent
                    : CreationFlags.Recreate;

                NetCourseEmitter.EmitPreview(ref ecb, in original, flags, entity);
                m_InPlace.Add(entity);
                cuts.Dispose();

                return;
            }

            m_Previewed.Add(new Previewed {
                Sloped = sloped,
                Start  = ElevationOf(edge.m_Start),
                Edge   = ElevationOf(entity),
                End    = ElevationOf(edge.m_End),
                OnPath = onPath
            });

            // Hidden and deleted, as the game leaves an edge it merges into another.
            NetCourseEmitter.EmitPreview(
                ref ecb,
                in original,
                CreationFlags.Delete | CreationFlags.Hidden,
                entity);

            var cutFlags = CoursePosFlags.DisableMerge | CoursePosFlags.IsRight;
            var whole    = new EdgeConfig {
                StartNodeEntity = startNode,
                EndNodeEntity   = endNode,
                Bezier          = sloped,
                Length          = MathUtils.Length(sloped),
                NetPrefabEntity = prefab,
                RandomSeed      = seed.m_Seed,
                Upgrades        = upgraded.m_Flags,
                StartNodeFlags  = startNode == Entity.Null
                    ? cutFlags
                    : CoursePosFlags.IsFirst | CoursePosFlags.IsRight,
                EndNodeFlags    = endNode == Entity.Null
                    ? cutFlags
                    : CoursePosFlags.IsLast | CoursePosFlags.IsRight
            };
            var pieces = new NativeList<EdgeConfig>(8, Allocator.Temp);
            var from   = 0f;

            for (var i = 0; i <= cuts.Length; i++) {
                var to = i < cuts.Length ? cuts[i] : 1f;

                TunnelRuns.AddRun(whole, from, to, geometry.m_ElevationLimit, cutFlags, ref pieces);
                from = to;
            }

            for (var i = 0; i < pieces.Length; i++) {
                var piece = pieces[i];

                // Cutting a curve at 0 or at 1 may not give back its end to the last bit.
                if (i == 0) {
                    piece.Bezier.a = sloped.a;
                }

                if (i == pieces.Length - 1) {
                    piece.Bezier.d = sloped.d;
                }

                piece.StartNodePosition = piece.Bezier.a;
                piece.EndNodePosition   = piece.Bezier.d;
                piece.StartNodeRotation = NetUtils.GetNodeRotation(
                    MathUtils.StartTangent(piece.Bezier));
                piece.EndNodeRotation   = NetUtils.GetNodeRotation(
                    MathUtils.EndTangent(piece.Bezier));
                NetCourseEmitter.EmitPreview(ref ecb, in piece, CreationFlags.SubElevation);
            }

            cuts.Dispose();
            pieces.Dispose();
        }

        /// <summary>
        ///     Gets the elevation an entity stores, none being zero.
        /// </summary>
        /// <param name="entity">A node or an edge.</param>
        /// <returns>The stored elevation.</returns>
        private float2 ElevationOf(Entity entity) {
            EntityManager.TryGetComponent<Elevation>(entity, out var elevation);

            return elevation.m_Elevation;
        }

        /// <summary>
        ///     Gives an edge previewed in place and its nodes the elevations the apply would store.
        ///     Its course names no node, and the game gives a node it makes no elevation.
        ///     An edge the slope does not move may still be one the apply makes a tunnel:
        ///     a road made level with Tunnel off lies in an open cut however deep it is.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="edgeEntity">The temporary edge.</param>
        /// <param name="original">The edge it stands for.</param>
        private void KeepStored(ref TerrainHeightData terrain, Entity edgeEntity, Entity original) {
            if (!m_InPlace.Contains(original)) {
                return;
            }

            var edge   = EntityManager.GetComponentData<Edge>(edgeEntity);
            var source = EntityManager.GetComponentData<Edge>(original);
            var prefab = EntityManager.GetComponentData<PrefabRef>(original).m_Prefab;

            if (!CanTunnel(prefab)) {
                KeepStoredNode(edge.m_Start, source.m_Start);
                KeepStoredNode(edge.m_End, source.m_End);

                return;
            }

            var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
            var bezier   = EntityManager.GetComponentData<Curve>(edgeEntity).m_Bezier;
            var atStart  = ElevationOf(source.m_Start);
            var atMiddle = ElevationOf(original);
            var atEnd    = ElevationOf(source.m_End);

            StorePreviewElevation(ref terrain, edge.m_Start, bezier, 0f, geometry, atStart);
            StorePreviewElevation(ref terrain, edgeEntity, bezier, 0.5f, geometry, atMiddle);
            StorePreviewElevation(ref terrain, edge.m_End, bezier, 1f, geometry, atEnd);
        }

        /// <summary>
        ///     Gives a temporary node the elevation of the node it lies on.
        /// </summary>
        /// <param name="node">The temporary node.</param>
        /// <param name="source">The node of the path.</param>
        private void KeepStoredNode(Entity node, Entity source) {
            if (!EntityManager.TryGetComponent<Elevation>(source, out var elevation)) {
                return;
            }

            if (!EntityManager.TryGetComponent<Elevation>(node, out var measured)) {
                EntityManager.AddComponentData(node, elevation);

                return;
            }

            // A piece laid again from the same node has measured it already.
            if (math.all(measured.m_Elevation == 0f)) {
                EntityManager.SetComponentData(node, elevation);
            }
        }

        /// <summary>
        ///     Checks whether the tunnel preview lays an edge of the path again in place.
        ///     <see cref="NT_TunnelMouthSystem" /> judges such a piece at the edge's nodes.
        /// </summary>
        /// <param name="original">The edge of the path.</param>
        /// <returns>True if the preview holds a piece for it, with no node named.</returns>
        internal bool PreviewsInPlace(Entity original) {
            return m_InPlace.Contains(original);
        }

        /// <summary>
        ///     Gives the pieces of the tunnel preview the elevations the apply would store.
        ///     The game measures them on the ground the path has shaped where it lies now.
        ///     The pieces of the path also take the outline upstream's preview gives the path.
        ///     The game outlines a temporary entity flagged as a parent in green.
        ///     It only takes that flag from a definition that has an original.
        ///     No piece is priced, as the apply is free.
        ///     Called by <see cref="NT_TunnelMouthSystem" /> before it checks the mouths.
        /// </summary>
        /// <param name="edges">The temporary edges of the preview.</param>
        internal void CompletePreview(NativeArray<Entity> edges) {
            var terrain = MapTerrain();

            for (var i = 0; i < edges.Length; i++) {
                var temp = EntityManager.GetComponentData<Temp>(edges[i]);

                if (temp.m_Original != Entity.Null) {
                    KeepStored(ref terrain, edges[i], temp.m_Original);

                    continue;
                }

                var bezier = EntityManager.GetComponentData<Curve>(edges[i]).m_Bezier;
                var middle = MathUtils.Position(bezier, 0.5f);
                var found  = -1;
                var at     = 0f;
                var off    = float.MaxValue;

                // The game grades a piece anew between its ends: on an arch the heights differ.
                // They only tell apart two curves that cross, one above the other.
                for (var j = 0; j < m_Previewed.Length; j++) {
                    var curve  = m_Previewed[j].Sloped;
                    var bounds = MathUtils.Expand(MathUtils.Bounds(curve.xz), 1f);

                    // The distance to a curve is costly.
                    if (!MathUtils.Intersect(bounds, middle.xz)) {
                        continue;
                    }

                    var distance = MathUtils.Distance(curve.xz, middle.xz, out var t);
                    var height   = math.abs(MathUtils.Position(curve, t).y - middle.y);

                    if (distance <= 1f && height < off) {
                        found = j;
                        at    = t;
                        off   = height;
                    }
                }

                if (found < 0) {
                    continue;
                }

                var previewed = m_Previewed[found];
                var sloped    = previewed.Sloped;

                // The apply charges nothing, and the game only prices an essential piece.
                temp.m_Flags &= ~TempFlags.Essential;

                if (previewed.OnPath) {
                    temp.m_Flags |= TempFlags.Parent;
                }

                EntityManager.SetComponentData(edges[i], temp);

                var prefab = EntityManager.GetComponentData<PrefabRef>(edges[i]).m_Prefab;

                if (!CanTunnel(prefab)) {
                    continue;
                }

                var edge     = EntityManager.GetComponentData<Edge>(edges[i]);
                var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);

                var atStart  = Existing(previewed, bezier.a);
                var atMiddle = previewed.Edge;
                var atEnd    = Existing(previewed, bezier.d);

                // The middle is measured on the curve the apply writes, not on the game's grade.
                StorePreviewElevation(ref terrain, edge.m_Start, bezier, 0f, geometry, atStart);
                StorePreviewElevation(ref terrain, edges[i], sloped, at, geometry, atMiddle);
                StorePreviewElevation(ref terrain, edge.m_End, bezier, 1f, geometry, atEnd);
            }
        }

        /// <summary>
        ///     Gets the elevation the apply would find at a node of the preview.
        ///     A node the cuts add takes after the edge, as it does once the game has split it.
        /// </summary>
        /// <param name="previewed">The edge the node's piece is laid for.</param>
        /// <param name="position">The position of the node.</param>
        /// <returns>The elevation there now.</returns>
        private static float2 Existing(Previewed previewed, float3 position) {
            MathUtils.Distance(previewed.Sloped.xz, position.xz, out var t);

            if (t < 0.01f) {
                return previewed.Start;
            }

            return t > 0.99f ? previewed.End : previewed.Edge;
        }

        /// <summary>
        ///     Stores on a temporary node or edge the elevation the apply would store there.
        ///     See <see cref="TunnelRuns.Stored" />.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="entity">The temporary node or edge that takes the elevation.</param>
        /// <param name="bezier">The curve of the temporary edge.</param>
        /// <param name="t">Curve position to measure at.</param>
        /// <param name="geometry">The geometry data of the edge's prefab.</param>
        /// <param name="existing">The elevation the apply would find there.</param>
        private void StorePreviewElevation(
            ref TerrainHeightData terrain,
            Entity                entity,
            Bezier4x3             bezier,
            float                 t,
            NetGeometryData       geometry,
            float2                existing) {
            var half      = geometry.m_DefaultWidth * 0.5f;
            var measured  = TunnelRuns.Elevation(ref terrain, bezier, t, half);
            var elevation = TunnelRuns.Stored(measured, existing, geometry.m_ElevationLimit);

            var had = EntityManager.HasComponent<Elevation>(entity);

            // The game keeps an elevation of zero only on a building's own network.
            if (math.any(elevation != 0f) || (had && EntityManager.HasComponent<Owner>(entity))) {
                EntityManager.AddComponentData(entity, new Elevation(elevation));
            } else if (had) {
                EntityManager.RemoveComponent<Elevation>(entity);
            }
        }

        /// <summary>
        ///     Slopes the branches once they are cut.
        ///     Each branch is found again from its junction, piece after piece along its curve.
        ///     Every piece takes its part of that curve and the elevations of a network laid there.
        ///     A branch that is not found whole keeps what the apply does of it.
        ///     Its first edge is then bent alone.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        private void SlopeBranches(TerrainHeightData terrain) {
            var ecb    = m_Barrier.CreateCommandBuffer();
            var pieces = new NativeList<Entity>(8, Allocator.Temp);
            var ends   = new NativeList<float>(8, Allocator.Temp);
            var all    = new NativeList<Entity>(8, Allocator.Temp);

            for (var i = 0; i < m_Branches.Length; i++) {
                var branch   = m_Branches[i];
                var node     = branch.Junction;
                var previous = Entity.Null;
                var from     = 0f;

                pieces.Clear();
                ends.Clear();

                while (from < 1f
                       && pieces.Length < 16
                       && NextPiece(node, previous, branch.Sloped, from, out var found)) {
                    var to = PlannedAt(branch.Sloped, found.To);

                    pieces.Add(found.Piece);
                    ends.Add(to);

                    previous = found.Piece;
                    node     = found.Next;
                    from     = to;
                }

                if (from < 1f) {
                    continue;
                }

                node = branch.Junction;

                for (var p = 0; p < pieces.Length; p++) {
                    var piece    = pieces[p];
                    var edge     = EntityManager.GetComponentData<Edge>(piece);
                    var prefab   = EntityManager.GetComponentData<PrefabRef>(piece).m_Prefab;
                    var geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);
                    var forward  = edge.m_Start == node;
                    var range    = new Bounds1(p == 0 ? 0f : ends[p - 1], ends[p]);
                    var bezier   = MathUtils.Cut(branch.Sloped, range);
                    var near     = node;

                    node = forward ? edge.m_End : edge.m_Start;

                    if (ends[p] < 1f) {
                        var moved = EntityManager.GetComponentData<Node>(node);

                        moved.m_Position = bezier.d;
                        ecb.SetComponent(node, moved);
                    }

                    if (CanTunnel(prefab)) {
                        // A node shared with the path keeps the elevation the path gives it.
                        if (p > 0) {
                            StoreElevation(ref ecb, ref terrain, near, bezier, 0f, geometry);
                        }

                        StoreElevation(ref ecb, ref terrain, piece, bezier, 0.5f, geometry);

                        if (ends[p] < 1f || !m_CurrentPathNodes.Contains(node)) {
                            StoreElevation(ref ecb, ref terrain, node, bezier, 1f, geometry);
                        }
                    }

                    if (!forward) {
                        bezier = MathUtils.Invert(bezier);
                    }

                    ecb.SetComponent(piece, new Curve {
                        m_Bezier = bezier,
                        m_Length = MathUtils.Length(bezier)
                    });
                    ecb.AddComponent<Updated>(piece);
                    ecb.AddComponent<BatchesUpdated>(piece);
                    ecb.AddComponent<Updated>(node);
                    ecb.AddComponent<BatchesUpdated>(node);
                    all.Add(piece);
                }
            }

            var mouthSystem = World.GetOrCreateSystemManaged<NT_TunnelMouthSystem>();

            mouthSystem.Rewritten(all.AsArray(), terrain);
            m_Branches.Clear();

            pieces.Dispose();
            ends.Dispose();
            all.Dispose();
        }

        /// <summary>
        ///     Gets where a node of a branch belongs: at the mouth the first pass cut there.
        ///     The game snapped that cut by up to <see cref="SnapReach" />, outward too.
        /// </summary>
        /// <param name="curve">The curve of the branch.</param>
        /// <param name="t">Curve position of the node.</param>
        /// <returns>The curve position of the mouth, or the node's if none was cut there.</returns>
        private float PlannedAt(Bezier4x3 curve, float t) {
            if (t >= 1f || !IsPlanned(MathUtils.Position(curve, t).xz, out var mouth)) {
                return t;
            }

            MathUtils.Distance(curve.xz, mouth, out var at);

            return at;
        }

        /// <summary>
        ///     The next piece of a branch, as <see cref="NextPiece" /> finds it.
        /// </summary>
        private struct FoundPiece {
            /// <summary>
            ///     The edge found.
            /// </summary>
            public Entity Piece;

            /// <summary>
            ///     The node the edge leads to.
            /// </summary>
            public Entity Next;

            /// <summary>
            ///     Curve position of that node on the branch's curve.
            /// </summary>
            public float To;
        }

        /// <summary>
        ///     Finds the edge that goes on along a branch's curve from a node.
        /// </summary>
        /// <param name="node">The node to go on from.</param>
        /// <param name="previous">The edge that led to the node, or null at the junction.</param>
        /// <param name="sloped">The curve of the branch.</param>
        /// <param name="from">Curve position of the node.</param>
        /// <param name="found">Output: the piece found.</param>
        /// <returns>True if an edge was found.</returns>
        private bool NextPiece(
            Entity         node,
            Entity         previous,
            Bezier4x3      sloped,
            float          from,
            out FoundPiece found) {
            var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true);

            for (var i = 0; i < connected.Length; i++) {
                var piece = connected[i].m_Edge;
                var edge  = EntityManager.GetComponentData<Edge>(piece);

                if (piece == previous
                    || m_CurrentPathEdges.Contains(piece)
                    || (edge.m_Start != node && edge.m_End != node)) {
                    continue;
                }

                var next     = edge.m_Start == node ? edge.m_End : edge.m_Start;
                var position = EntityManager.GetComponentData<Node>(next).m_Position;

                if (MathUtils.Distance(sloped.xz, position.xz, out var to) < 1f && to > from) {
                    found = new FoundPiece {
                        Piece = piece,
                        Next  = next,
                        To    = to > 0.999f ? 1f : to
                    };

                    return true;
                }
            }

            found = default;

            return false;
        }

        /// <summary>
        ///     Stores on an entity the elevation of a curve written in place.
        ///     See <see cref="TunnelRuns.Stored" />.
        /// </summary>
        /// <param name="ecb">Command buffer to write with.</param>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="entity">The node or edge that takes the elevation.</param>
        /// <param name="bezier">The curve to measure on.</param>
        /// <param name="t">Curve position to measure at.</param>
        /// <param name="geometry">The geometry data of the edge's prefab.</param>
        private void StoreElevation(
            ref EntityCommandBuffer ecb,
            ref TerrainHeightData   terrain,
            Entity                  entity,
            Bezier4x3               bezier,
            float                   t,
            NetGeometryData         geometry) {
            var limit     = geometry.m_ElevationLimit;
            var half      = geometry.m_DefaultWidth * 0.5f;
            var measured  = TunnelRuns.Elevation(ref terrain, bezier, t, half);
            var had       = EntityManager.TryGetComponent<Elevation>(entity, out var existing);
            var elevation = TunnelRuns.Stored(measured, existing.m_Elevation, limit);

            // The game keeps an elevation of zero only on a building's own network.
            if (math.any(elevation != 0f) || (had && EntityManager.HasComponent<Owner>(entity))) {
                ecb.AddComponent(entity, new Elevation(elevation));
            } else if (had) {
                ecb.RemoveComponent<Elevation>(entity);
            }
        }

        /// <summary>
        ///     Finds the path between the selected nodes again.
        ///     The cuts have added nodes and replaced edges on it.
        /// </summary>
        /// <returns>True if the path reaches every selected node.</returns>
        private bool FindPathAgain() {
            var found = true;
            var nodes = new NativeList<Entity>(32, Allocator.Temp);
            var edges = new NativeList<Entity>(32, Allocator.Temp);

            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();

            for (var i = 1; i < m_SelectedNodes.Length; i++) {
                found &= FindPathBetween(
                    m_SelectedNodes[i - 1],
                    m_SelectedNodes[i],
                    ref nodes,
                    ref edges);

                for (var j = i == 1 ? 0 : 1; j < nodes.Length; j++) {
                    m_CurrentPathNodes.Add(nodes[j]);
                }

                m_CurrentPathEdges.AddRange(edges.AsArray());
            }

            nodes.Dispose();
            edges.Dispose();

            return found;
        }
    }
}
