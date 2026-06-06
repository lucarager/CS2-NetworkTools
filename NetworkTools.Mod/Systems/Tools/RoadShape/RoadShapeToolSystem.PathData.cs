namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Prefabs;

    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// Partial class containing cached path data management.
    /// Path data is gathered via sync Burst job when selection changes,
    /// then used by handles and preview/apply jobs.
    /// </summary>
    public partial class NT_RoadShapeToolSystem {
        #region Cached Path Data Fields

        /// <summary>
        /// Cached transform context, populated by RefreshPathData().
        /// Contains path start/end positions, total length, and config.
        /// </summary>
        private ShapeTransformContext m_ShapeTransformContext;

        /// <summary>
        /// Cached edge states, populated by RefreshPathData().
        /// Contains per-edge beziers, lengths, and cumulative distances.
        /// </summary>
        private NativeList<EdgeState> m_EdgeStates;

        /// <summary>
        /// Cached node states, populated by RefreshPathData().
        /// Contains per-node positions tracked independently from edge bezier endpoints.
        /// Length is always CurrentPathNodes.Length (edges + 1).
        /// </summary>
        private NativeList<NodeState> m_NodeStates;

        /// <summary>
        /// Whether the cached path data is valid for use.
        /// Set false when selection changes, set true after RefreshPathData().
        /// </summary>
        private bool m_PathDataValid;

        #endregion

        #region Path Data Refresh

        /// <summary>
        /// Gathers transform context and edge states via sync Burst job.
        /// Called whenever entering Ready phase or when path changes while in Ready.
        /// Stores results in m_ShapeTransformContext and m_EdgeStates.
        /// </summary>
        private void RefreshPathData() {
            if (m_SelectedNodes.Length < 2 || m_CurrentPathEdges.Length == 0) {
                m_Log.Debug("RefreshPathData: Insufficient selection, skipping");
                m_PathDataValid = false;
                return;
            }

            m_Log.Debug($"RefreshPathData: Gathering data for {m_CurrentPathEdges.Length} edges");

            // Prepare output containers
            var contextRef = new NativeReference<ShapeTransformContext>(Allocator.TempJob);

            // Resize edge states if needed
            if (m_EdgeStates.Capacity < m_CurrentPathEdges.Length) {
                m_EdgeStates.SetCapacity(m_CurrentPathEdges.Length);
            }
            m_EdgeStates.Clear();
            m_EdgeStates.Resize(m_CurrentPathEdges.Length, NativeArrayOptions.ClearMemory);

            // Resize node states (N+1 nodes for N edges)
            var nodeCount = m_CurrentPathNodes.Length;
            if (m_NodeStates.Capacity < nodeCount) {
                m_NodeStates.SetCapacity(nodeCount);
            }
            m_NodeStates.Clear();
            m_NodeStates.Resize(nodeCount, NativeArrayOptions.ClearMemory);

            // Run sync Burst job on main thread
            new GatherPathDataJob {
                SelectedNodes = m_SelectedNodes,
                CurrentPathNodes = m_CurrentPathNodes,
                CurrentPathEdges = m_CurrentPathEdges,
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup = SystemAPI.GetComponentLookup<Upgraded>(true),
                OutContext = contextRef,
                OutEdgeStates = m_EdgeStates,
                OutNodeStates = m_NodeStates,
            }.Run();

            // Copy result to cached field
            m_ShapeTransformContext = contextRef.Value;
            contextRef.Dispose();

            m_PathDataValid = true;
            m_Log.Debug($"RefreshPathData: Gathered {m_EdgeStates.Length} edges, TotalLength={m_ShapeTransformContext.TotalLength:F2}");

        }

        /// <summary>
        /// Invalidates cached path data. Called when selection changes.
        /// </summary>
        private void InvalidatePathData() {
            m_PathDataValid = false;
        }

        #endregion

        #region Handle Refresh

        /// <summary>
        /// Creates or refreshes transform handles using cached path data.
        /// Called when entering Ready phase or when config/path changes.
        /// </summary>
        private void RefreshTransformHandles() {
            if (!m_PathDataValid || m_EdgeStates.Length == 0) {
                m_Log.Debug("RefreshTransformHandles: Path data not ready, skipping");
                return;
            }

            m_Log.Debug($"RefreshTransformHandles: Creating handles for template {Template.Value}");
            RebuildHandlesForActiveMode();
        }

        #endregion

        #region GatherPathDataJob

        /// <summary>
        /// Burst-compiled job that gathers edge states and creates transform context.
        /// Runs synchronously on main thread via .Run() for immediate availability.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        internal struct GatherPathDataJob : IJob {
            [ReadOnly] public NativeList<Entity> SelectedNodes;
            [ReadOnly] public NativeList<Entity> CurrentPathNodes;
            [ReadOnly] public NativeList<Entity> CurrentPathEdges;
            [ReadOnly] public ComponentLookup<Node> NodeLookup;
            [ReadOnly] public ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public ComponentLookup<Upgraded> UpgradedLookup;

            public NativeReference<ShapeTransformContext> OutContext;
            public NativeList<EdgeState> OutEdgeStates;
            public NativeList<NodeState> OutNodeStates;

            public void Execute() {
                // 1. InitializeConfig context from path endpoints
                var startPos = NodeLookup[SelectedNodes[0]].m_Position;
                var endPos = NodeLookup[SelectedNodes[^1]].m_Position;

                var ctx = ShapeTransformContext.Create(startPos, endPos);

                // 2. Gather node states (N+1 nodes for N edges)
                var nodeCount = CurrentPathNodes.Length;
                for (var i = 0; i < nodeCount; i++) {
                    var nodeEntity = CurrentPathNodes[i];
                    var nodePos = NodeLookup.TryGetComponent(nodeEntity, out var node)
                        ? node.m_Position
                        : float3.zero;

                    OutNodeStates[i] = new NodeState {
                        Entity = nodeEntity,
                        PathIndex = i,
                        Position = nodePos,
                        OriginalPosition = nodePos
                    };
                }

                // 3. Gather edge states
                var edgeCount = CurrentPathEdges.Length;
                var cumulativeDistance = 0f;

                // First pass: gather edge data and calculate total length (including node-to-bezier gaps)
                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var state = new EdgeState {
                        EdgeEntity = edgeEntity,
                        PathIndex = i,
                    };

                    // Get edge component for direction and node references
                    if (EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        state.StartNode = edge.m_Start;
                        state.EndNode = edge.m_End;

                        var currentNode = CurrentPathNodes[i];
                        state.IsForward = edge.m_Start == currentNode;
                    }

                    state.NetworkComposition = GetNetworkComposition(edgeEntity);

                    // Get curve component for geometry
                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        state.Bezier = curve.m_Bezier;
                        state.Length = curve.m_Length;
                        state.CalculateControlPointRatios();

                        // Store original bezier endpoints for node position delta calculations
                        state.OriginalBezierA = curve.m_Bezier.a;
                        state.OriginalBezierD = curve.m_Bezier.d;
                    }

                    // Account for gap from previous edge's path-end to node[i]
                    if (i > 0) {
                        var prevEdge = OutEdgeStates[i - 1];
                        var prevPathEnd = prevEdge.IsForward ? prevEdge.Bezier.d : prevEdge.Bezier.a;
                        cumulativeDistance += math.distance(prevPathEnd, OutNodeStates[i].OriginalPosition);
                    }

                    // Account for gap from node[i] to this edge's path-start
                    var pathStartBezier = state.IsForward ? state.Bezier.a : state.Bezier.d;
                    cumulativeDistance += math.distance(OutNodeStates[i].OriginalPosition, pathStartBezier);

                    state.CumulativeDistance = cumulativeDistance;

                    OutEdgeStates[i] = state;
                    cumulativeDistance += state.Length;
                }

                // Add trailing gap from last edge's path-end to end node
                if (edgeCount > 0) {
                    var lastEdge = OutEdgeStates[edgeCount - 1];
                    var lastPathEnd = lastEdge.IsForward ? lastEdge.Bezier.d : lastEdge.Bezier.a;
                    cumulativeDistance += math.distance(lastPathEnd, OutNodeStates[edgeCount].OriginalPosition);
                }

                // Update context with total length
                ctx.TotalLength = cumulativeDistance;

                // Second pass: calculate absolute ratios for each control point
                if (ctx.TotalLength > 0f) {
                    for (var i = 0; i < edgeCount; i++) {
                        var edge = OutEdgeStates[i];

                        edge.StartPointAbsoluteRatio = edge.CumulativeDistance / ctx.TotalLength;
                        edge.EndPointAbsoluteRatio = (edge.CumulativeDistance + edge.Length) / ctx.TotalLength;
                        edge.StartControlPointAbsoluteRatio =
                            (edge.CumulativeDistance + edge.StartControlPointRatio * edge.Length) / ctx.TotalLength;
                        edge.EndControlPointAbsoluteRatio =
                            (edge.CumulativeDistance + edge.EndControlPointRatio * edge.Length) / ctx.TotalLength;

                        OutEdgeStates[i] = edge;
                    }
                }

                // Output context
                OutContext.Value = ctx;
            }

            /// <summary>
            /// Gets the network composition from an entity's Upgraded component.
            /// </summary>
            private NetworkComposition GetNetworkComposition(Entity entity) {
                if (!UpgradedLookup.TryGetComponent(entity, out var upgraded)) {
                    return NetworkComposition.None;
                }

                if ((upgraded.m_Flags.m_General & CompositionFlags.General.Elevated) != 0) {
                    return NetworkComposition.Elevated;
                }

                if ((upgraded.m_Flags.m_General & CompositionFlags.General.Tunnel) != 0) {
                    return NetworkComposition.Tunnel;
                }

                return NetworkComposition.Ground;
            }
        }

        #endregion
    }
}
