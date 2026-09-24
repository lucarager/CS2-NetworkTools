namespace NetworkTools.Systems.Tools {
    using Colossal.Entities;
    using Colossal.Mathematics;

    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tunnel mode of the Connect, Slope, and Parallel tools.
    ///     Makes sure a tunnel ends only where the whole width of the network is deep enough.
    ///     Its head wall must be level too (<see cref="TunnelRuns.IsMouth" />).
    ///     The game reads an edge's elevation at its start, its middle, and its end.
    ///     Three limits across the width at one, and two on one side at all three, make a tunnel.
    ///     On a hillside that puts the mouth where one side is in the open.
    ///     The head wall leaves a gap there.
    ///     The system turns such an edge into an open cut.
    ///     It lifts the edge's own elevation just above the tunnel threshold.
    ///     The next edge inwards follows, up to the node placed for the mouth.
    ///     An edge where the ground dips close to it becomes an open cut too: the roof would show.
    ///     An edge that is deep at its middle stays a tunnel unless it is short.
    ///     All the ground above it would go otherwise.
    ///     Runs on the tool's temporary entities, before the composition selection.
    ///     It runs once for a preview, in the frame the game generates it.
    ///     The preview shows the result, and the built network inherits it.
    ///     It completes the Slope tool's preview first.
    ///     See <see cref="NT_RoadShapeToolSystem.CompletePreview" />.
    ///     Also runs on the edges the Slope tool rewrote in place during the frame.
    /// </summary>
    public partial class NT_TunnelMouthSystem : GameSystemBase {
        /// <summary>
        ///     What the Slope tool's preview lays for the built edges and nodes around it.
        /// </summary>
        private struct StandIns {
            /// <summary>
            ///     True for the Slope tool's preview.
            /// </summary>
            public bool Active;

            /// <summary>
            ///     The copy of each built edge the preview lays again.
            /// </summary>
            public NativeHashMap<Entity, Entity> Copies;

            /// <summary>
            ///     The built edges the preview hides, laying new pieces for them.
            /// </summary>
            public NativeHashSet<Entity> Hidden;

            /// <summary>
            ///     The temporary node the new pieces join, for each built node.
            /// </summary>
            public NativeHashMap<Entity, Entity> Nodes;
        }

        private ToolSystem             m_ToolSystem;
        private TerrainSystem          m_TerrainSystem;
        private NT_ConnectToolSystem   m_ConnectTool;
        private NT_RoadShapeToolSystem m_SlopeTool;
        private NT_ParallelToolSystem  m_ParallelTool;
        private EntityQuery            m_TempEdgeQuery;
        private EntityQuery            m_PieceQuery;

        /// <summary>
        ///     Edges the Slope tool rewrote in place, judged at the next update.
        ///     A command buffer that plays back before then writes their elevations.
        /// </summary>
        private NativeList<Entity> m_Rewritten;

        /// <summary>
        ///     Terrain on which the tool measured the elevations of <see cref="m_Rewritten" />.
        /// </summary>
        private TerrainHeightData m_RewrittenTerrain;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem    = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ConnectTool   = World.GetOrCreateSystemManaged<NT_ConnectToolSystem>();
            m_SlopeTool     = World.GetOrCreateSystemManaged<NT_RoadShapeToolSystem>();
            m_ParallelTool  = World.GetOrCreateSystemManaged<NT_ParallelToolSystem>();
            m_Rewritten     = new NativeList<Entity>(32, Allocator.Persistent);
            m_TempEdgeQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Temp, Updated, Edge, Curve, Elevation, PrefabRef>()
                                       .Build();
            m_PieceQuery    = SystemAPI.QueryBuilder()
                                       .WithAll<Temp, Updated, Edge, Curve, PrefabRef>()
                                       .Build();
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            m_Rewritten.Dispose();
            base.OnDestroy();
        }

        /// <summary>
        ///     Registers edges rewritten in place this frame, elevations included.
        ///     A command buffer of the tool phase does the writing.
        /// </summary>
        /// <param name="edges">The rewritten edges.</param>
        /// <param name="terrain">The terrain on which the tool measured their elevations.</param>
        public void Rewritten(NativeArray<Entity> edges, TerrainHeightData terrain) {
            m_Rewritten.AddRange(edges);
            m_RewrittenTerrain = terrain;
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_Rewritten.Length > 0) {
                Demote(m_Rewritten.AsArray(), m_RewrittenTerrain, false, true);
                m_Rewritten.Clear();
            }

            var connectTunnel  = m_ToolSystem.activeTool == m_ConnectTool
                                 && m_ConnectTool.Tunnel.Value;
            var slopeTunnel    = m_ToolSystem.activeTool == m_SlopeTool
                                 && m_SlopeTool.PreviewsTunnels;
            var parallelTunnel = m_ToolSystem.activeTool == m_ParallelTool
                                 && m_ParallelTool.Tunnel.Value;

            // The Slope tool's pieces may have no elevation yet.
            if (slopeTunnel && !m_PieceQuery.IsEmptyIgnoreFilter) {
                var pieces = m_PieceQuery.ToEntityArray(Allocator.Temp);

                m_SlopeTool.CompletePreview(pieces);
                pieces.Dispose();
            }

            if ((connectTunnel || slopeTunnel || parallelTunnel)
                && !m_TempEdgeQuery.IsEmptyIgnoreFilter) {
                // The Slope tool's preview lies where a road has shaped the ground already.
                var terrain = slopeTunnel
                    ? m_SlopeTool.MapTerrain()
                    : m_TerrainSystem.GetHeightData(true);
                var edges = m_TempEdgeQuery.ToEntityArray(Allocator.Temp);

                Demote(edges, terrain, true, slopeTunnel);
                edges.Dispose();
            }
        }

        /// <summary>
        ///     Turns tunnel edges that end at a bad mouth, or that hold a dip, into open cuts.
        ///     Repeats until nothing changes: demoting an edge moves the mouth to the next node.
        /// </summary>
        /// <param name="edges">The edges to check.</param>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="temp">True when the edges are temporary entities of a preview.</param>
        /// <param name="inPlace">True when the tool rewrites its network in place.</param>
        private void Demote(
            NativeArray<Entity> edges,
            TerrainHeightData   terrain,
            bool                temp,
            bool                inPlace) {
            var standIns = new StandIns {
                Active = temp && inPlace,
                Copies = new NativeHashMap<Entity, Entity>(16, Allocator.Temp),
                Hidden = new NativeHashSet<Entity>(16, Allocator.Temp),
                Nodes  = new NativeHashMap<Entity, Entity>(16, Allocator.Temp)
            };

            if (standIns.Active) {
                CollectStandIns(ref standIns);
            }

            for (var pass = 0; pass < edges.Length; pass++) {
                var changed = false;

                for (var i = 0; i < edges.Length; i++) {
                    var judged    = edges[i];
                    var judgeTemp = temp;

                    // A preview also holds copies of the networks around it: those stay as built.
                    // A piece of the path laid again in place names no node.
                    // Its own nodes join none of the built network.
                    // It is judged at the nodes of the edge it stands for.
                    if (temp && IsCopy(edges[i])) {
                        judged = EntityManager.GetComponentData<Temp>(edges[i]).m_Original;

                        if (!inPlace || !m_SlopeTool.PreviewsInPlace(judged)) {
                            continue;
                        }

                        judgeTemp = false;
                    } else if (temp && !IsLaid(edges[i])) {
                        // A built edge the preview splits at a junction comes back as new pieces.
                        // They have no original, and stay as built too.
                        continue;
                    }

                    if (!IsTunnel(edges[i], out var geometry)) {
                        continue;
                    }

                    var edge   = EntityManager.GetComponentData<Edge>(judged);
                    var bezier = EntityManager.GetComponentData<Curve>(edges[i]).m_Bezier;
                    var limit  = geometry.m_ElevationLimit;
                    var depth  = limit * 3f - TunnelRuns.Tolerance;
                    var half   = geometry.m_DefaultWidth * 0.5f;

                    // An open cut keeps no ground above that depth.
                    // Only a short edge may become one while deep at its middle.
                    var deepMiddle = TunnelRuns.Cover(ref terrain, bezier, 0.5f, half) >= depth;

                    if (deepMiddle && MathUtils.Length(bezier) > TunnelRuns.MaxDeepCutLength) {
                        continue;
                    }

                    // A node judged, not placed: it is allowed the tolerance a placed one keeps.
                    var slack   = TunnelRuns.Tolerance;
                    var startOk = !EndsAt(edge.m_Start, judged, judgeTemp, standIns)
                        || TunnelRuns.IsMouth(ref terrain, bezier, 0f, half, limit, slack);
                    var endOk = !EndsAt(edge.m_End, judged, judgeTemp, standIns)
                        || TunnelRuns.IsMouth(ref terrain, bezier, 1f, half, limit, slack);

                    if (startOk && endOk && !HasDip(ref terrain, bezier, geometry)) {
                        continue;
                    }

                    // The lift stops just above the threshold from which the game makes a tunnel.
                    var cutElevation = 0.1f - limit * 2f;
                    var elevation    = EntityManager.GetComponentData<Elevation>(edges[i]);

                    // Nothing to lift: a network whose open cuts are tunnels too stays one.
                    if (math.all(elevation.m_Elevation >= cutElevation)) {
                        continue;
                    }

                    elevation.m_Elevation = math.max(elevation.m_Elevation, cutElevation);
                    EntityManager.SetComponentData(edges[i], elevation);
                    changed = true;
                }

                if (!changed) {
                    break;
                }
            }

            standIns.Copies.Dispose();
            standIns.Hidden.Dispose();
            standIns.Nodes.Dispose();
        }

        /// <summary>
        ///     Finds what the Slope tool's preview lays for the built edges and nodes around it.
        /// </summary>
        /// <param name="standIns">The copies, hidden edges, and temporary nodes, filled.</param>
        private void CollectStandIns(ref StandIns standIns) {
            var pieces = m_PieceQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < pieces.Length; i++) {
                var temp = EntityManager.GetComponentData<Temp>(pieces[i]);
                var edge = EntityManager.GetComponentData<Edge>(pieces[i]);

                if (temp.m_Original != Entity.Null && (temp.m_Flags & TempFlags.Delete) != 0) {
                    standIns.Hidden.Add(temp.m_Original);
                } else if (temp.m_Original != Entity.Null) {
                    standIns.Copies.TryAdd(temp.m_Original, pieces[i]);
                }

                AddStandIn(ref standIns, edge.m_Start);
                AddStandIn(ref standIns, edge.m_End);
            }

            pieces.Dispose();
        }

        /// <summary>
        ///     Notes the built node a temporary node stands for, if any.
        /// </summary>
        /// <param name="standIns">The stand-ins, updated.</param>
        /// <param name="node">A node of a temporary edge.</param>
        private void AddStandIn(ref StandIns standIns, Entity node) {
            if (EntityManager.TryGetComponent<Temp>(node, out var temp)
                && temp.m_Original != Entity.Null) {
                standIns.Nodes.TryAdd(temp.m_Original, node);
            }
        }

        /// <summary>
        ///     Checks whether a temporary edge stands for an edge that exists.
        /// </summary>
        /// <param name="edgeEntity">The temporary edge to check.</param>
        /// <returns>True if the edge has an original.</returns>
        private bool IsCopy(Entity edgeEntity) {
            return EntityManager.GetComponentData<Temp>(edgeEntity).m_Original != Entity.Null;
        }

        /// <summary>
        ///     Checks whether a temporary edge is one the tool lays, not a piece of a built one.
        /// </summary>
        /// <param name="edgeEntity">The temporary edge to check.</param>
        /// <returns>True if the preview creates the edge.</returns>
        private bool IsLaid(Entity edgeEntity) {
            return (EntityManager.GetComponentData<Temp>(edgeEntity).m_Flags
                    & TempFlags.Create) != 0;
        }

        /// <summary>
        ///     Checks whether the game makes a tunnel of an edge.
        ///     The elevations of the edge and of its two nodes decide.
        /// </summary>
        /// <param name="edgeEntity">The edge to check.</param>
        /// <param name="geometry">Output: the geometry data of the edge's prefab.</param>
        /// <returns>True if the edge gets a tunnel composition.</returns>
        private bool IsTunnel(Entity edgeEntity, out NetGeometryData geometry) {
            geometry = default;

            var edge   = EntityManager.GetComponentData<Edge>(edgeEntity);
            var prefab = EntityManager.GetComponentData<PrefabRef>(edgeEntity).m_Prefab;

            if (!EntityManager.HasComponent<Elevation>(edgeEntity)
                || !EntityManager.HasComponent<Elevation>(edge.m_Start)
                || !EntityManager.HasComponent<Elevation>(edge.m_End)
                || !EntityManager.HasComponent<NetGeometryData>(prefab)) {
                return false;
            }

            geometry = EntityManager.GetComponentData<NetGeometryData>(prefab);

            var flags = NetCompositionHelpers.GetElevationFlags(
                EntityManager.GetComponentData<Elevation>(edge.m_Start),
                EntityManager.GetComponentData<Elevation>(edgeEntity),
                EntityManager.GetComponentData<Elevation>(edge.m_End),
                geometry);

            return (flags.m_General & CompositionFlags.General.Tunnel) != 0;
        }

        /// <summary>
        ///     Checks whether a tunnel ends at a node: none of the node's other edges is a tunnel.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <param name="tunnelEdge">The tunnel edge that reaches the node.</param>
        /// <param name="temp">True when only a temporary node can be a mouth.</param>
        /// <param name="standIns">What the Slope tool's preview lays for the built network.</param>
        /// <returns>True if the node is a mouth of the tunnel.</returns>
        private bool EndsAt(Entity node, Entity tunnelEdge, bool temp, StandIns standIns) {
            if (temp && !EntityManager.HasComponent<Temp>(node)) {
                return false;
            }

            if (standIns.Active) {
                return EndsInPreview(node, tunnelEdge, temp, standIns);
            }

            var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true)
                                         .ToNativeArray(Allocator.Temp);
            var others  = 0;
            var tunnels = 0;

            for (var i = 0; i < connected.Length; i++) {
                var other = connected[i].m_Edge;

                // A preview keeps the edges it replaces, hidden and deleted.
                if (other == tunnelEdge || IsDeleted(other)) {
                    continue;
                }

                others++;

                if (IsTunnel(other, out _)) {
                    tunnels++;
                }
            }

            connected.Dispose();

            return others > 0 && tunnels == 0;
        }

        /// <summary>
        ///     Checks whether a tunnel of the Slope tool's preview ends at a node.
        ///     The preview keeps the built edges it lays again, hidden and deleted.
        ///     An edge laid again in place names no node, so it joins nodes of its own.
        ///     So the node is judged by the built one's edges, each as the preview lays it:
        ///     its copy, or the new pieces laid for it, which join the preview's node.
        /// </summary>
        /// <param name="node">The node to check, built or temporary.</param>
        /// <param name="tunnelEdge">The tunnel edge that reaches the node.</param>
        /// <param name="temp">True when the node is the preview's own.</param>
        /// <param name="standIns">What the preview lays for the built network.</param>
        /// <returns>True if the node is a mouth of the tunnel.</returns>
        private bool EndsInPreview(Entity node, Entity tunnelEdge, bool temp, StandIns standIns) {
            var built    = node;
            var standIn  = Entity.Null;
            var original = tunnelEdge;

            if (temp) {
                standIn  = node;
                built    = EntityManager.GetComponentData<Temp>(node).m_Original;
                original = EntityManager.GetComponentData<Temp>(tunnelEdge).m_Original;
            } else {
                standIns.Nodes.TryGetValue(node, out standIn);
            }

            var others  = 0;
            var tunnels = 0;

            if (built != Entity.Null) {
                var edges = EntityManager.GetBuffer<ConnectedEdge>(built, true)
                                         .ToNativeArray(Allocator.Temp);

                for (var i = 0; i < edges.Length; i++) {
                    var other = edges[i].m_Edge;

                    // A hidden edge is counted by the pieces laid for it, below.
                    if (other == original || standIns.Hidden.Contains(other)) {
                        continue;
                    }

                    if (standIns.Copies.TryGetValue(other, out var copy)) {
                        other = copy;
                    }

                    others++;

                    if (IsTunnel(other, out _)) {
                        tunnels++;
                    }
                }

                edges.Dispose();
            }

            if (standIn != Entity.Null) {
                var pieces = EntityManager.GetBuffer<ConnectedEdge>(standIn, true)
                                          .ToNativeArray(Allocator.Temp);

                for (var i = 0; i < pieces.Length; i++) {
                    var piece = pieces[i].m_Edge;
                    var copy  = built != Entity.Null && IsCopy(piece);

                    if (piece == tunnelEdge || copy || IsDeleted(piece)) {
                        continue;
                    }

                    others++;

                    if (IsTunnel(piece, out _)) {
                        tunnels++;
                    }
                }

                pieces.Dispose();
            }

            return others > 0 && tunnels == 0;
        }

        /// <summary>
        ///     Checks whether an edge is a temporary one that its preview deletes.
        /// </summary>
        /// <param name="edgeEntity">The edge to check.</param>
        /// <returns>True if the edge is flagged for deletion.</returns>
        private bool IsDeleted(Entity edgeEntity) {
            return EntityManager.HasComponent<Temp>(edgeEntity)
                   && (EntityManager.GetComponentData<Temp>(edgeEntity).m_Flags
                       & TempFlags.Delete) != 0;
        }

        /// <summary>
        ///     Checks whether the ground dips close to the network somewhere inside an edge.
        ///     Such an edge is the cut left between two tunnels.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve of the edge.</param>
        /// <param name="geometry">The geometry data of the edge's prefab.</param>
        /// <returns>True if the ground cover goes under the elevation limit.</returns>
        private static bool HasDip(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            NetGeometryData       geometry) {
            var samples = TunnelRuns.SampleCount(MathUtils.Length(bezier));
            var half    = geometry.m_DefaultWidth * 0.5f;
            var dip     = geometry.m_ElevationLimit - TunnelRuns.Tolerance;

            for (var i = 1; i < samples - 1; i++) {
                if (TunnelRuns.Cover(ref terrain, bezier, i / (float)(samples - 1), half) < dip) {
                    return true;
                }
            }

            return false;
        }
    }
}
