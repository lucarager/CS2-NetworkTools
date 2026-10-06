namespace NetworkTools.Systems.Tools.Connect {
    using System.Text;

    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Partial class containing the test hooks, called from a debugger or a test scenario.
    ///     The curve parameters are public and refresh the preview when set.
    ///     The source generator lands a copy of another file's job method under these usings.
    ///     A test is <see cref="DebugSelect" />, parameter writes, then <see cref="DebugDump" />.
    /// </summary>
    public partial class NT_ConnectToolSystem {
        /// <summary>
        ///     The side flags a dump shows: a lowered or raised side, and the transitions to one.
        /// </summary>
        private const CompositionFlags.Side SideMask = CompositionFlags.Side.Lowered
            | CompositionFlags.Side.Raised
            | CompositionFlags.Side.LowTransition
            | CompositionFlags.Side.HighTransition;

        /// <summary>
        ///     True while the tool lays a curve with no node selected, from a debugger.
        ///     The definitions job then runs without the two nodes and takes the prefab as set.
        /// </summary>
        private bool m_DebugFree;

        /// <summary>
        ///     Point a dump keeps to the edges near, when its radius is set.
        /// </summary>
        private float3 m_DebugNear;

        /// <summary>
        ///     Radius of a dump around its point, zero for the whole map.
        /// </summary>
        private float m_DebugRadius;

        /// <summary>
        ///     Lays the simple curve with no node selected, on a map without any.
        ///     With Tunnel off this places a plain road.
        /// </summary>
        /// <param name="road">Name of the road prefab ("Small Road").</param>
        /// <param name="a">Start of the curve.</param>
        /// <param name="b">Handle of the start.</param>
        /// <param name="c">Handle of the end.</param>
        /// <param name="d">End of the curve.</param>
        public void DebugFree(string road, float3 a, float3 b, float3 c, float3 d) {
            DebugFree(nameof(RoadPrefab), road, a, b, c, d);
        }

        /// <summary>
        ///     Lays the simple curve with no node selected, with any network.
        /// </summary>
        /// <param name="type">Type of the prefab ("RoadPrefab", "TrackPrefab").</param>
        /// <param name="name">Name of the prefab.</param>
        /// <param name="a">Start of the curve.</param>
        /// <param name="b">Handle of the start.</param>
        /// <param name="c">Handle of the end.</param>
        /// <param name="d">End of the curve.</param>
        public void DebugFree(string type, string name, float3 a, float3 b, float3 c, float3 d) {
            DebugStart(type, name, ConnectMode.SimpleCurve);

            CurveStartPointPosition.Value        = a;
            CurveEndPointPosition.Value          = d;
            CurveStartControlPointPosition.Value = b;
            CurveEndControlPointPosition.Value   = c;
        }

        /// <summary>
        ///     Starts any mode with any network and no node selected.
        ///     The caller writes the mode's parameters afterwards.
        ///     A point goes before the handles that depend on it.
        /// </summary>
        /// <param name="type">Type of the prefab ("RoadPrefab", "TrackPrefab").</param>
        /// <param name="name">Name of the prefab.</param>
        /// <param name="mode">The mode to start.</param>
        public void DebugStart(string type, string name, ConnectMode mode) {
            var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();

            prefabs.TryGetPrefab(new PrefabID(type, name), out var prefab);
            ResetToIdle();
            NetPrefab.Set(prefab, prefabs.GetEntity(prefab), Entity.Null);

            Mode.Value     = mode;
            m_DebugFree    = true;
            Phase          = OperationPhase.Ready;
            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Selects two nodes as two clicks would.
        /// </summary>
        /// <param name="start">The first node.</param>
        /// <param name="end">The second node.</param>
        public void DebugSelect(Entity start, Entity end) {
            m_DebugFree = false;

            ResetToIdle();
            HandleAddNode(start);
            HandleAddNode(end);

            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Selects the nodes nearest to two points, as two clicks would.
        ///     The tool lays its default curve between them.
        /// </summary>
        /// <param name="start">A point near the first node.</param>
        /// <param name="end">A point near the second node.</param>
        public void DebugSelectNear(float3 start, float3 end) {
            var nodes = SystemAPI.QueryBuilder()
                .WithAll<Node, ConnectedEdge>()
                .WithNone<Deleted, Temp>()
                .Build()
                .ToEntityArray(Allocator.Temp);
            var first = Entity.Null;
            var last  = Entity.Null;

            for (var i = 0; i < nodes.Length; i++) {
                var position = EntityManager.GetComponentData<Node>(nodes[i]).m_Position;

                if (first == Entity.Null
                    || math.distance(position, start) < DistanceTo(first, start)) {
                    first = nodes[i];
                }

                if (last == Entity.Null || math.distance(position, end) < DistanceTo(last, end)) {
                    last = nodes[i];
                }
            }

            nodes.Dispose();
            DebugSelect(first, last);
        }

        /// <summary>
        ///     Selects two nodes and lays the simple curve anywhere.
        ///     The ends are set first, so that their handles follow them.
        /// </summary>
        /// <param name="start">The first node.</param>
        /// <param name="end">The second node.</param>
        /// <param name="a">Start of the curve.</param>
        /// <param name="b">Handle of the start.</param>
        /// <param name="c">Handle of the end.</param>
        /// <param name="d">End of the curve.</param>
        public void DebugCurve(Entity start, Entity end, float3 a, float3 b, float3 c, float3 d) {
            DebugSelect(start, end);

            CurveStartPointPosition.Value        = a;
            CurveEndPointPosition.Value          = d;
            CurveStartControlPointPosition.Value = b;
            CurveEndControlPointPosition.Value   = c;
        }

        /// <summary>
        ///     Lays the simple curve between the first two nodes of the map.
        ///     Entity numbers change from one load to the next, so no call takes any.
        /// </summary>
        /// <param name="a">Start of the curve.</param>
        /// <param name="b">Handle of the start.</param>
        /// <param name="c">Handle of the end.</param>
        /// <param name="d">End of the curve.</param>
        public void DebugCurve(float3 a, float3 b, float3 c, float3 d) {
            var nodes = SystemAPI.QueryBuilder()
                .WithAll<Node, ConnectedEdge>()
                .WithNone<Deleted, Temp>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            DebugCurve(nodes[0], nodes[1], a, b, c, d);
            nodes.Dispose();
        }

        /// <summary>
        ///     Bulldozes one edge, and each of its nodes that it leaves without an edge.
        /// </summary>
        /// <param name="edgeEntity">The edge to delete.</param>
        public void DebugDelete(Entity edgeEntity) {
            var edge = EntityManager.GetComponentData<Edge>(edgeEntity);

            EntityManager.AddComponent<Deleted>(edgeEntity);
            DebugRelease(edge.m_Start, edgeEntity);
            DebugRelease(edge.m_End, edgeEntity);
        }

        /// <summary>
        ///     Dumps one line per edge, the preview's or the built ones.
        ///     A line holds the ends, the heights of the four control points, length, and flags.
        ///     Then the stored elevations of the start node, the edge, and the end node.
        ///     Then the ground at the start, the middle, and the end.
        ///     The ground is read above the left edge, the centre, and the right edge.
        /// </summary>
        /// <param name="temp">True for the preview's edges, false for the built ones.</param>
        /// <returns>The lines, one per edge.</returns>
        public string DebugDump(bool temp) {
            return DebugDump(temp, false);
        }

        /// <summary>
        ///     Dumps the full lines of the edges starting within a distance of a point.
        ///     A map with a city on it has too many edges for the whole dump.
        /// </summary>
        /// <param name="temp">True for the preview's edges, false for the built ones.</param>
        /// <param name="near">The point.</param>
        /// <param name="radius">How far from the point an edge may start.</param>
        /// <returns>The lines, one per edge.</returns>
        public string DebugDump(bool temp, float3 near, float radius) {
            m_DebugNear   = near;
            m_DebugRadius = radius;

            var text = DebugDump(temp, false);

            m_DebugRadius = 0f;

            return text;
        }

        /// <summary>
        ///     Dumps the edges, in full or in brief.
        ///     Brief is the edges off the ground only: ends, length, flags, ground at both ends.
        /// </summary>
        /// <param name="temp">True for the preview's edges, false for the built ones.</param>
        /// <param name="brief">True for the brief lines.</param>
        /// <returns>The lines, one per edge.</returns>
        public string DebugDump(bool temp, bool brief) {
            var query = temp
                ? SystemAPI.QueryBuilder()
                    .WithAll<Edge, Curve, Composition, PrefabRef, Temp>()
                    .WithNone<Deleted>()
                    .Build()
                : SystemAPI.QueryBuilder()
                    .WithAll<Edge, Curve, Composition, PrefabRef>()
                    .WithNone<Deleted, Temp>()
                    .Build();
            var edges   = query.ToEntityArray(Allocator.Temp);
            var terrain = m_TerrainSystem.GetHeightData(true);
            var text    = new StringBuilder();

            for (var i = 0; i < edges.Length; i++) {
                var edge   = EntityManager.GetComponentData<Edge>(edges[i]);
                var bezier = EntityManager.GetComponentData<Curve>(edges[i]).m_Bezier;

                if (m_DebugRadius > 0f
                    && math.distance(bezier.a.xz, m_DebugNear.xz) > m_DebugRadius) {
                    continue;
                }

                var composition = EntityManager.GetComponentData<Composition>(edges[i]);
                var flags       = FlagsOf(composition.m_Edge);
                var prefab      = EntityManager.GetComponentData<PrefabRef>(edges[i]).m_Prefab;
                var half        = EntityManager.HasComponent<NetGeometryData>(prefab)
                    ? EntityManager.GetComponentData<NetGeometryData>(prefab).m_DefaultWidth * 0.5f
                    : 0f;
                var a           = bezier.a;
                var d           = bezier.d;
                var ends        = $"({a.x:0},{a.z:0})>({d.x:0},{d.z:0})";
                var length      = MathUtils.Length(bezier);
                var groundStart = Ground(ref terrain, bezier, 0f, half);
                var groundMid   = Ground(ref terrain, bezier, 0.5f, half);
                var groundEnd   = Ground(ref terrain, bezier, 1f, half);

                if (brief) {
                    if (flags != "0/0/0") {
                        text.Append($"{ends} {length:0} m {flags} {groundStart} {groundEnd}\n");
                    }

                    continue;
                }

                text.Append($"{edges[i].Index} {edge.m_Start.Index}>{edge.m_End.Index} {ends}");
                text.Append($" y {a.y:0.00} {bezier.b.y:0.00} {bezier.c.y:0.00} {d.y:0.00}");
                text.Append($" len {length:0} {flags}");
                text.Append($" nodes {FlagsOf(composition.m_StartNode)}");
                text.Append($" {FlagsOf(composition.m_EndNode)}");
                text.Append($" elev {Stored(edge.m_Start)} {Stored(edges[i])}");
                text.Append($" {Stored(edge.m_End)}");
                text.Append($" ground {groundStart} {groundMid} {groundEnd}\n");
            }

            edges.Dispose();

            return text.ToString();
        }

        /// <summary>
        ///     Dumps one line per placeable network.
        ///     A line holds the prefab type and name, the elevation range, the underground prefab.
        ///     Then the elevation limit, the width, how many sections and pieces ask for Tunnel.
        ///     Last, the geometry flags that matter to a tunnel.
        /// </summary>
        /// <returns>The lines, one per network.</returns>
        public string DebugNets() {
            const GeometryFlags shown = GeometryFlags.LoweredIsTunnel
                | GeometryFlags.RaisedIsElevated
                | GeometryFlags.RequireElevated
                | GeometryFlags.ElevatedIsRaised
                | GeometryFlags.Marker
                | GeometryFlags.OnWater;

            var prefabs  = World.GetOrCreateSystemManaged<PrefabSystem>();
            var entities = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlaceableNetData, NetGeometryData, PrefabData>()
                .Build(EntityManager)
                .ToEntityArray(Allocator.Temp);
            var text     = new StringBuilder();

            for (var i = 0; i < entities.Length; i++) {
                if (!prefabs.TryGetPrefab<NetGeometryPrefab>(entities[i], out var prefab)) {
                    continue;
                }

                var placeable   = EntityManager.GetComponentData<PlaceableNetData>(entities[i]);
                var geometry    = EntityManager.GetComponentData<NetGeometryData>(entities[i]);
                var underground = placeable.m_UndergroundPrefab != Entity.Null
                    ? prefabs.GetPrefabName(placeable.m_UndergroundPrefab)
                    : "-";
                var range       = placeable.m_ElevationRange;
                var sections    = 0;
                var pieces      = 0;

                foreach (var section in prefab.m_Sections) {
                    if (AsksTunnel(section.m_RequireAll) || AsksTunnel(section.m_RequireAny)) {
                        sections++;
                    }

                    if (section.m_Section?.m_Pieces == null) {
                        continue;
                    }

                    foreach (var piece in section.m_Section.m_Pieces) {
                        if (AsksTunnel(piece.m_RequireAll) || AsksTunnel(piece.m_RequireAny)) {
                            pieces++;
                        }
                    }
                }

                var extra = prefab.GetComponent<UndergroundNetSections>()?.m_Sections.Length ?? 0;

                text.Append($"{prefab.GetType().Name} | {prefab.name}");
                text.Append($" | range [{range.min:0} {range.max:0}] | under {underground}");
                text.Append($" | limit {geometry.m_ElevationLimit:0.#}");
                text.Append($" | width {geometry.m_DefaultWidth:0.#}");
                text.Append($" | tunnel sections {sections} pieces {pieces}");
                text.Append($" underground sections {extra}");
                text.Append($" | {geometry.m_Flags & shown}\n");
            }

            entities.Dispose();

            return text.ToString();
        }

        /// <summary>
        ///     Checks whether a set of piece requirements asks for Tunnel.
        /// </summary>
        /// <param name="requirements">The requirements, or null.</param>
        /// <returns>True if Tunnel is among them.</returns>
        private static bool AsksTunnel(NetPieceRequirements[] requirements) {
            return requirements != null
                && System.Array.IndexOf(requirements, NetPieceRequirements.Tunnel) >= 0;
        }

        /// <summary>
        ///     Writes the flags of a composition as a dump shows them.
        ///     The parts are the general flags, the left side, and the right side.
        /// </summary>
        /// <param name="flags">The flags.</param>
        /// <returns>The three parts, separated by slashes.</returns>
        private static string Flags(CompositionFlags flags) {
            var general = flags.m_General
                & (CompositionFlags.General.Tunnel | CompositionFlags.General.Elevated);

            return $"{general}/{flags.m_Left & SideMask}/{flags.m_Right & SideMask}";
        }

        /// <summary>
        ///     Measures the ground above a point of a curve.
        ///     It is read at the left edge, the centre, and the right edge of the network.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="t">Curve position of the point.</param>
        /// <param name="half">Half the width of the network.</param>
        /// <returns>The three heights, in brackets.</returns>
        private static string Ground(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 t,
            float                 half) {
            var position = MathUtils.Position(bezier, t);
            var side     = new float3 {
                xz = math.normalizesafe(MathUtils.Right(MathUtils.Tangent(bezier, t).xz)) * half,
            };
            var left     = TerrainUtils.SampleHeight(ref terrain, position - side) - position.y;
            var centre   = TerrainUtils.SampleHeight(ref terrain, position) - position.y;
            var right    = TerrainUtils.SampleHeight(ref terrain, position + side) - position.y;

            return $"[{left:0.0} {centre:0.0} {right:0.0}]";
        }

        /// <summary>
        ///     Deletes a node that the deletion of an edge leaves without any, or updates it.
        ///     Every other edge of the node is updated too.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="deletedEdge">The edge being deleted.</param>
        private void DebugRelease(Entity node, Entity deletedEdge) {
            var connected = EntityManager.GetBuffer<ConnectedEdge>(node, true)
                .ToNativeArray(Allocator.Temp);
            var orphan    = true;

            for (var i = 0; i < connected.Length; i++) {
                var other = connected[i].m_Edge;

                if (other == deletedEdge || EntityManager.HasComponent<Deleted>(other)) {
                    continue;
                }

                orphan = false;
                EntityManager.AddComponent<Updated>(other);
            }

            if (orphan) {
                EntityManager.AddComponent<Deleted>(node);
            } else {
                EntityManager.AddComponent<Updated>(node);
            }

            connected.Dispose();
        }

        /// <summary>
        ///     Measures the distance from a node to a point.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="point">The point.</param>
        /// <returns>The distance.</returns>
        private float DistanceTo(Entity node, float3 point) {
            return math.distance(EntityManager.GetComponentData<Node>(node).m_Position, point);
        }

        /// <summary>
        ///     Writes the flags of a composition entity as a dump shows them.
        /// </summary>
        /// <param name="composition">The composition entity.</param>
        /// <returns>The three parts, separated by slashes.</returns>
        private string FlagsOf(Entity composition) {
            return Flags(EntityManager.GetComponentData<NetCompositionData>(composition).m_Flags);
        }

        /// <summary>
        ///     Writes the elevations an entity stores, or a dash when it stores none.
        /// </summary>
        /// <param name="entity">The node or edge.</param>
        /// <returns>The two elevations in brackets, or a dash.</returns>
        private string Stored(Entity entity) {
            if (!EntityManager.HasComponent<Elevation>(entity)) {
                return "-";
            }

            var elevation = EntityManager.GetComponentData<Elevation>(entity).m_Elevation;

            return $"[{elevation.x:0.0} {elevation.y:0.0}]";
        }
    }
}
