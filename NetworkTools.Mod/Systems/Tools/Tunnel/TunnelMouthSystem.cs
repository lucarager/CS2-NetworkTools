namespace NetworkTools.Systems.Tools {
    using Colossal.Mathematics;

    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tunnel mode of the Connect tool.
    ///     Makes sure a tunnel ends only where the whole width of the network is deep enough.
    ///     The game makes a tunnel of an edge as soon as one of its nodes is deep enough.
    ///     The other node only has to be deep on one side.
    ///     On a hillside that puts the mouth where one side is in the open.
    ///     The head wall leaves a gap there.
    ///     Such an edge is turned into an open cut.
    ///     Its own elevation is lifted just above the tunnel threshold.
    ///     The next edge inwards follows, up to the node placed for the mouth.
    ///     The cut left between two tunnels at a dip of the ground is treated the same way.
    ///     An edge that is deep at its middle stays a tunnel unless it is short.
    ///     All the ground above it would go otherwise.
    ///     Runs on the tool's temporary entities, before the composition selection.
    ///     It runs once for a preview, in the frame the game generates it.
    ///     The preview shows the result, and the built network inherits it.
    /// </summary>
    public partial class NT_TunnelMouthSystem : GameSystemBase {
        private ToolSystem           m_ToolSystem;
        private TerrainSystem        m_TerrainSystem;
        private NT_ConnectToolSystem m_ConnectTool;
        private EntityQuery          m_TempEdgeQuery;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem    = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ConnectTool   = World.GetOrCreateSystemManaged<NT_ConnectToolSystem>();
            m_TempEdgeQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Temp, Updated, Edge, Curve, Elevation, PrefabRef>()
                                       .Build();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var connectTunnel = m_ToolSystem.activeTool == m_ConnectTool
                                && m_ConnectTool.Tunnel.Value;

            if (connectTunnel && !m_TempEdgeQuery.IsEmptyIgnoreFilter) {
                var edges = m_TempEdgeQuery.ToEntityArray(Allocator.Temp);

                Demote(edges, m_TerrainSystem.GetHeightData(true), true);
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
        private void Demote(NativeArray<Entity> edges, TerrainHeightData terrain, bool temp) {
            for (var pass = 0; pass < edges.Length; pass++) {
                var changed = false;

                for (var i = 0; i < edges.Length; i++) {
                    // A preview also holds copies of the networks around it: those stay as built
                    if (temp && IsCopy(edges[i])) {
                        continue;
                    } else if (temp && !IsLaid(edges[i])) {
                        // A built edge the preview splits at a junction comes back as new pieces.
                        // They have no original, and stay as built too.
                        continue;
                    }

                    if (!IsTunnel(edges[i], out var geometry)) {
                        continue;
                    }

                    var edge   = EntityManager.GetComponentData<Edge>(edges[i]);
                    var bezier = EntityManager.GetComponentData<Curve>(edges[i]).m_Bezier;
                    var depth  = geometry.m_ElevationLimit * 3f - TunnelRuns.Tolerance;
                    var half   = geometry.m_DefaultWidth * 0.5f;

                    // An open cut keeps no ground above that depth, so an edge that is deep
                    // at its middle is never made one, unless it is a short portal edge.
                    var deepMiddle = TunnelRuns.Cover(ref terrain, bezier, 0.5f, half) >= depth;

                    if (deepMiddle && MathUtils.Length(bezier) > TunnelRuns.MaxDeepCutLength) {
                        continue;
                    }

                    var startOk = !IsMouth(edge.m_Start, edges[i], temp)
                                  || TunnelRuns.Cover(ref terrain, bezier, 0f, half) >= depth;
                    var endOk = !IsMouth(edge.m_End, edges[i], temp)
                                || TunnelRuns.Cover(ref terrain, bezier, 1f, half) >= depth;

                    if (startOk && endOk && !HasDip(ref terrain, bezier, geometry)) {
                        continue;
                    }

                    // Just above the threshold from which the game makes a tunnel.
                    var cutElevation = 0.1f - geometry.m_ElevationLimit * 2f;
                    var elevation    = EntityManager.GetComponentData<Elevation>(edges[i]);

                    // Nothing to lift: a network whose open cuts are tunnels too stays one
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
        /// <returns>True if the node is a mouth of the tunnel.</returns>
        private bool IsMouth(Entity node, Entity tunnelEdge, bool temp) {
            if (temp && !EntityManager.HasComponent<Temp>(node)) {
                return false;
            }

            var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true)
                                         .ToNativeArray(Allocator.Temp);
            var mouth = connected.Length > 1;

            for (var i = 0; i < connected.Length; i++) {
                if (connected[i].m_Edge != tunnelEdge && IsTunnel(connected[i].m_Edge, out _)) {
                    mouth = false;
                }
            }

            connected.Dispose();

            return mouth;
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
