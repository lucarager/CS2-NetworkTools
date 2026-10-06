namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_RoadShapeToolSystem {
#if USE_BURST
        [BurstCompile]
#endif
        internal struct ShapeTransformJob : IJob {
            [ReadOnly] public required NativeList<EdgeState>             EdgeStates;
            [ReadOnly] public required NativeList<NodeState>             NodeStates;
            [ReadOnly] public required ShapeTransformContext             Context;
            [ReadOnly] public required ShapeJobConfig                     Config;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
            [ReadOnly] public required ComponentLookup<Elevation>        ElevationLookup;
            [ReadOnly] public required ComponentLookup<Owner>            OwnerLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData>  NetGeometryDataLookup;
            [ReadOnly] public required ComponentLookup<PlaceableNetData> PlaceableNetDataLookup;
            [ReadOnly] public required TerrainHeightData                 TerrainHeight;
            public required            ToolOutputMode                    OutputMode;
            public required            EntityCommandBuffer               ECB;

            /// <summary>
            ///     Minimum height delta (in meters) to consider for intersection adjustments.
            /// </summary>
            private const float HeightDeltaThreshold = 0.001f;

            /// <summary>
            ///     Minimum XZ delta squared (in meters²) to consider for intersection adjustments.
            /// </summary>
            private const float XZDeltaSquaredThreshold = 0.000001f;

            public void Execute() {
                if (EdgeStates.Length == 0) {
                    return;
                }

                // 1. Copy cached data to mutable arrays for transform pipeline
                var edges = new NativeArray<EdgeState>(EdgeStates.Length, Allocator.Temp);
                for (var i = 0; i < EdgeStates.Length; i++) {
                    edges[i] = EdgeStates[i];
                }

                var nodes = new NativeArray<NodeState>(NodeStates.Length, Allocator.Temp);
                for (var i = 0; i < NodeStates.Length; i++) {
                    nodes[i] = NodeStates[i];
                }

                // 2. Execute transformation (context = path geometry, config = user settings)
                Transform(ref edges, ref nodes, in Context, in Config);

                // 3. Write slope metadata to edge entities (preview only — Apply resets the tool
                //    immediately, so ECB additions would outlive the tool session).
                if (OutputMode == ToolOutputMode.Preview) {
                    OutputMetadata(edges);
                }

                // 4. Output
                if (OutputMode == ToolOutputMode.Preview)
                {
                    // Tunnel mode lays a preview of its own, see EmitTunnelPreview
                    if (!Config.Tunnel) {
                        OutputPreview(edges, nodes);
                    }
                } else
                {
                    OutputApply(edges, nodes);
                }

                // Cleanup
                edges.Dispose();
                nodes.Dispose();
            }

            /// <summary>
            ///     Applies the configured template to the path.
            ///     Static so that the tunnel apply can run it on the main thread.
            /// </summary>
            /// <param name="edges">Edge states of the path, transformed in place.</param>
            /// <param name="nodes">Node states of the path, transformed in place.</param>
            /// <param name="context">Transform context of the path.</param>
            /// <param name="config">Job configuration: the template and its parameters.</param>
            public static void Transform(
                ref NativeArray<EdgeState> edges,
                ref NativeArray<NodeState> nodes,
                in ShapeTransformContext   context,
                in ShapeJobConfig          config) {
                switch (config.Template) {
                    case ShapeTransformTemplate.SlopeLinear:
                        var linearTransform = new SlopeLinearTransform();
                        TransformPipeline.Execute(ref linearTransform, ref edges, ref nodes, in context, in config);
                        break;
                    case ShapeTransformTemplate.SlopeEaseInOut:
                        var easeInOutTransform = new SlopeEaseInOutTransform();
                        TransformPipeline.Execute(ref easeInOutTransform, ref edges, ref nodes, in context, in config);
                        break;
                    case ShapeTransformTemplate.SlopeArch:
                        var archTransform = new SlopeArchTransform();
                        TransformPipeline.Execute(ref archTransform, ref edges, ref nodes, in context, in config);
                        break;
                    case ShapeTransformTemplate.CurveStraighten:
                        var straightenTransform = new CurveStraightenTransform();
                        TransformPipeline.Execute(ref straightenTransform, ref edges, ref nodes, in context, in config);
                        break;
                    case ShapeTransformTemplate.CurveSmooth:
                        var smoothTransform = new CurveSmoothTransform();
                        TransformPipeline.Execute(ref smoothTransform, ref edges, ref nodes, in context, in config);
                        break;
                }
            }

            /// <summary>
            ///     Gets the network composition from an entity's Upgraded component.
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

            /// <summary>
            ///     Returns true if the node position has changed significantly in height or XZ.
            /// </summary>
            private bool HasNodePositionChanged(Entity nodeEntity, float3 newPosition) {
                if (!NodeLookup.TryGetComponent(nodeEntity, out var node)) {
                    return false;
                }

                if (math.abs(newPosition.y - node.m_Position.y) >= HeightDeltaThreshold) {
                    return true;
                }

                var xzDelta = newPosition.xz - node.m_Position.xz;
                return math.lengthsq(xzDelta) >= XZDeltaSquaredThreshold;
            }

            /// <summary>
            ///     Writes NT_Metadata (existing and new slope) to each selected edge entity.
            /// </summary>
            private void OutputMetadata(NativeArray<EdgeState> edges) {
                for (var i = 0; i < edges.Length; i++) {
                    var edge = edges[i];
                    var existingDeltaY = math.abs(edge.OriginalBezierD.y - edge.OriginalBezierA.y);
                    var existingSlope = edge.Length > 0.01f
                        ? existingDeltaY / edge.Length * 100f
                        : 0f;

                    var newDeltaY = math.abs(edge.Bezier.d.y - edge.Bezier.a.y);
                    var newLength = MathUtils.Length(edge.Bezier);
                    var newSlope = newLength > 0.01f
                        ? newDeltaY / newLength * 100f
                        : 0f;

                    ECB.AddComponent(edge.EdgeEntity, new NT_Metadata {
                        ExistingSlope = existingSlope,
                        NewSlope = newSlope,
                    });
                }
            }

            /// <summary>
            ///     Creates CreationDefinition + NetCourse entities for preview.
            /// </summary>
            private void OutputPreview(NativeArray<EdgeState> edges, NativeArray<NodeState> nodes) {
                var processedNodes = new NativeHashSet<Entity>(nodes.Length, Allocator.Temp);
                var nodePositionMap = new NativeHashMap<Entity, float3>(nodes.Length, Allocator.Temp);
                for (var i = 0; i < nodes.Length; i++) {
                    nodePositionMap.TryAdd(nodes[i].Entity, nodes[i].Position);
                }

                // Output selected edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    var startNodePos = nodePositionMap.TryGetValue(state.StartNode, out var snp) ? snp : state.Bezier.a;
                    var endNodePos   = nodePositionMap.TryGetValue(state.EndNode, out var enp)   ? enp : state.Bezier.d;
                    OutputPreviewEdge(state.EdgeEntity,
                                      state.Bezier,
                                      MathUtils.Length(state.Bezier),
                                      state.NetworkComposition,
                                      Entity.Null,
                                      Entity.Null,
                                      startNodePos,
                                      endNodePos);
                }

                // Output connected edges at each node
                for (var i = 0; i < nodes.Length; i++)
                {
                    var node = nodes[i];

                    if (processedNodes.Add(node.Entity))
                    {
                        PreviewConnectedEdges(
                            node.Entity,
                            node.Position,
                            edges,
                            nodePositionMap,
                            processedNodes);
                    }
                }

                processedNodes.Dispose();
                nodePositionMap.Dispose();
            }

            /// <summary>
            ///     Creates preview entities for edges connected to a node that are not in the selection.
            /// </summary>
            private void PreviewConnectedEdges(
                Entity                        nodeEntity,
                float3                        nodePosition,
                NativeArray<EdgeState>        selectedEdges,
                NativeHashMap<Entity, float3> nodePositionMap,
                NativeHashSet<Entity>         processedNodes) {
                //if (!HasNodePositionChanged(nodeEntity, nodePosition)) {
                //    return;
                //}

                if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
                    return;
                }

                for (var i = 0; i < connectedEdges.Length; i++) {
                    var connectedEdgeEntity = connectedEdges[i].m_Edge;

                    if (IsEdgeInSelection(connectedEdgeEntity, selectedEdges)) {
                        continue;
                    }

                    OutputPreviewConnectedEdge(
                        connectedEdgeEntity,
                        nodeEntity,
                        nodePositionMap,
                        processedNodes);
                }
            }

            /// <summary>
            ///     Creates a preview entity for a connected edge with adjusted control points at the intersection.
            ///     Applies the node movement delta to the bezier endpoint and control point,
            ///     preserving the original offset between node center and bezier endpoint.
            ///     An edge joining two selected nodes is laid once, from the first of them.
            /// </summary>
            private void OutputPreviewConnectedEdge(
                Entity                        edgeEntity,
                Entity                        nodeEntity,
                NativeHashMap<Entity, float3> nodePositionMap,
                NativeHashSet<Entity>         processedNodes) {
                if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                    return;
                }

                if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                    return;
                }

                if (edge.m_Start != nodeEntity && edge.m_End != nodeEntity) {
                    return;
                }

                var other = edge.m_Start == nodeEntity ? edge.m_End : edge.m_Start;

                if (other != nodeEntity && processedNodes.Contains(other)) {
                    return;
                }

                var bezier       = curve.m_Bezier;
                var startNodeRef = edge.m_Start;
                var endNodeRef   = edge.m_End;
                var startNodePos = NodeLookup.TryGetComponent(edge.m_Start, out var startNode)
                    ? startNode.m_Position
                    : bezier.a;
                var endNodePos   = NodeLookup.TryGetComponent(edge.m_End, out var endNode)
                    ? endNode.m_Position
                    : bezier.d;

                // Shift each end at a selected node by that node's movement delta.
                if (nodePositionMap.TryGetValue(edge.m_Start, out var movedStart)) {
                    var startDelta = movedStart - startNodePos;

                    bezier.a     += startDelta;
                    bezier.b     += startDelta;
                    startNodeRef =  Entity.Null;
                    startNodePos =  movedStart;
                }

                if (nodePositionMap.TryGetValue(edge.m_End, out var movedEnd)) {
                    var endDelta = movedEnd - endNodePos;

                    bezier.d   += endDelta;
                    bezier.c   += endDelta;
                    endNodeRef =  Entity.Null;
                    endNodePos =  movedEnd;
                }

                var composition = GetNetworkComposition(edgeEntity);
                OutputPreviewEdge(edgeEntity,
                                  bezier,
                                  MathUtils.Length(bezier),
                                  composition,
                                  startNodeRef,
                                  endNodeRef,
                                  startNodePos,
                                  endNodePos, 
                                  false);
            }

            /// <summary>
            ///     Creates a preview entity for an edge with configurable node references.
            /// </summary>
            private void OutputPreviewEdge(
                Entity             edgeEntity,
                Bezier4x3          bezier,
                float              length,
                NetworkComposition composition,
                Entity             startNodeEntity,
                Entity             endNodeEntity,
                float3             startNodePosition,
                float3             endNodePosition,
                bool showAsParent = true) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = edgeEntity,
                    m_Flags    = CreationFlags.Recreate
                };

                if (showAsParent) {
                    creationDefinition.m_Flags |= CreationFlags.Parent;
                }

                if (PrefabRefLookup.TryGetComponent(edgeEntity, out var prefabRef)) {
                    creationDefinition.m_Prefab = prefabRef;
                }

                if (PseudoRandomSeedLookup.TryGetComponent(edgeEntity, out var seed)) {
                    creationDefinition.m_RandomSeed = seed.m_Seed;
                }

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags = GetFlagsFromComposition(composition);
                var endNodeFlags   = GetFlagsFromComposition(composition);

                // FreeHeight tells the game to respect our custom heights
                startNodeFlags |= CoursePosFlags.FreeHeight | CoursePosFlags.IsGrid | CoursePosFlags.IsRight;
                endNodeFlags   |= CoursePosFlags.FreeHeight | CoursePosFlags.IsGrid | CoursePosFlags.IsRight;

                // Add flags to force connections
                if (startNodeEntity != Entity.Null && endNodeEntity == Entity.Null) {
                    startNodeFlags |= CoursePosFlags.IsFirst | CoursePosFlags.IsGrid;
                    endNodeFlags |= CoursePosFlags.IsLast | CoursePosFlags.IsGrid;
                } else if (endNodeEntity != Entity.Null && startNodeEntity == Entity.Null) {
                    endNodeFlags |= CoursePosFlags.IsFirst | CoursePosFlags.IsGrid;
                    startNodeFlags |= CoursePosFlags.IsLast | CoursePosFlags.IsGrid;
                }

                // Initialize elevations from what the edge and its nodes store: Apply keeps those,
                // so the preview gets the same ground/elevated/tunnel pieces as the result
                var startElevation = float2.zero;
                var endElevation = float2.zero;
                var courseElevation = float2.zero;

                if (EdgeLookup.TryGetComponent(edgeEntity, out var originalEdge)) {
                    if (ElevationLookup.TryGetComponent(originalEdge.m_Start, out var atStart)) {
                        startElevation = atStart.m_Elevation;
                    }

                    if (ElevationLookup.TryGetComponent(originalEdge.m_End, out var atEnd)) {
                        endElevation = atEnd.m_Elevation;
                    }
                }

                if (ElevationLookup.TryGetComponent(edgeEntity, out var atEdge)) {
                    courseElevation = atEdge.m_Elevation;
                }

                var netCourse = new NetCourse {
                    m_Curve      = bezier,
                    m_Length     = length,
                    m_FixedIndex = -1,
                    m_Elevation  = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = startNodeEntity,
                        m_Position      = startNodePosition,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = endNodeEntity,
                        m_Position      = endNodePosition,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier)),
                        m_CourseDelta   = 1,
                        m_Elevation     = endElevation,
                        m_Flags         = endNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    }
                };

                // Apply composition constraints (ground/tunnel/elevated)
                ApplyCompositionToNetCourse(ref netCourse, composition);

                ECB.AddComponent(definitionEntity, netCourse);
            }

            /// <summary>
            ///     Applies network composition constraints to a NetCourse.
            ///     Ground: forces elevation to 0.
            ///     Tunnel: ensures elevation is at most the tunnel threshold.
            ///     Elevated: ensures elevation is at least the elevated threshold.
            /// </summary>
            private static void ApplyCompositionToNetCourse(ref NetCourse netCourse, NetworkComposition composition) {
                switch (composition) {
                    case NetworkComposition.Ground:
                        netCourse.m_Elevation = SlopeUtils.ForceGroundElevation;
                        netCourse.m_StartPosition.m_Elevation = SlopeUtils.ForceGroundElevation;
                        netCourse.m_EndPosition.m_Elevation = SlopeUtils.ForceGroundElevation;
                        break;

                    case NetworkComposition.Tunnel:
                        netCourse.m_Elevation.x = math.min(netCourse.m_Elevation.x, SlopeUtils.TunnelThreshold.x);
                        netCourse.m_Elevation.y = math.min(netCourse.m_Elevation.y, SlopeUtils.TunnelThreshold.y);
                        netCourse.m_StartPosition.m_Elevation.x = math.min(netCourse.m_StartPosition.m_Elevation.x, SlopeUtils.TunnelThreshold.x);
                        netCourse.m_StartPosition.m_Elevation.y = math.min(netCourse.m_StartPosition.m_Elevation.y, SlopeUtils.TunnelThreshold.y);
                        netCourse.m_EndPosition.m_Elevation.x = math.min(netCourse.m_EndPosition.m_Elevation.x, SlopeUtils.TunnelThreshold.x);
                        netCourse.m_EndPosition.m_Elevation.y = math.min(netCourse.m_EndPosition.m_Elevation.y, SlopeUtils.TunnelThreshold.y);
                        break;

                    case NetworkComposition.Elevated:
                        netCourse.m_Elevation.x = math.max(netCourse.m_Elevation.x, SlopeUtils.ElevatedThreshold.x);
                        netCourse.m_Elevation.y = math.max(netCourse.m_Elevation.y, SlopeUtils.ElevatedThreshold.y);
                        netCourse.m_StartPosition.m_Elevation.x = math.max(netCourse.m_StartPosition.m_Elevation.x, SlopeUtils.ElevatedThreshold.x);
                        netCourse.m_StartPosition.m_Elevation.y = math.max(netCourse.m_StartPosition.m_Elevation.y, SlopeUtils.ElevatedThreshold.y);
                        netCourse.m_EndPosition.m_Elevation.x = math.max(netCourse.m_EndPosition.m_Elevation.x, SlopeUtils.ElevatedThreshold.x);
                        netCourse.m_EndPosition.m_Elevation.y = math.max(netCourse.m_EndPosition.m_Elevation.y, SlopeUtils.ElevatedThreshold.y);
                        break;
                }
            }

            /// <summary>
            ///     Gets the flags for a network composition.
            /// </summary>
            private static CoursePosFlags GetFlagsFromComposition(NetworkComposition composition) {
                return composition switch {
                    NetworkComposition.Elevated => CoursePosFlags.ForceElevatedEdge | CoursePosFlags.ForceElevatedNode,
                    NetworkComposition.Tunnel   => 0,
                    NetworkComposition.Ground   => 0,
                    _                           => 0
                };
            }

            /// <summary>
            ///     Applies transformation changes to existing Curve components, node positions, and intersection adjustments.
            /// </summary>
            private void OutputApply(NativeArray<EdgeState> edges, NativeArray<NodeState> nodes) {
                var processedNodes = new NativeHashSet<Entity>(nodes.Length, Allocator.Temp);

                // Apply curve changes to selected edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    ECB.SetComponent(state.EdgeEntity,
                                     new Curve {
                                         m_Bezier = state.Bezier,
                                         m_Length = MathUtils.Length(state.Bezier)
                                     });
                    if (Config.Tunnel) {
                        OutputUnderground(state);
                    }
                }

                // Update nodes and connected edges
                for (var i = 0; i < nodes.Length; i++) {
                    var node = nodes[i];

                    if (processedNodes.Add(node.Entity)) {
                        UpdateNodeAndConnectedEdges(node.Entity, node.Position, edges, nodes);
                    }
                }

                processedNodes.Dispose();
            }

            /// <summary>
            ///     Tunnel mode: stores the elevations of an edge and of its two nodes.
            ///     A curve written in place otherwise keeps the elevations it had.
            ///     A ground road then stays one however deep it goes.
            /// </summary>
            /// <param name="state">The transformed edge.</param>
            private void OutputUnderground(EdgeState state) {
                if (!PrefabRefLookup.TryGetComponent(state.EdgeEntity, out var prefabRef)) {
                    return;
                }

                if (!NetGeometryDataLookup.TryGetComponent(prefabRef.m_Prefab, out var geometry)) {
                    return;
                }

                if (!PlaceableNetDataLookup.TryGetComponent(prefabRef.m_Prefab, out var placeable)
                    || !TunnelRuns.CanTunnel(placeable)) {
                    return;
                }

                OutputUnderground(state.StartNode, state.Bezier, 0f, geometry);
                OutputUnderground(state.EdgeEntity, state.Bezier, 0.5f, geometry);
                OutputUnderground(state.EndNode, state.Bezier, 1f, geometry);
            }

            /// <summary>
            ///     Stores on a node or an edge the elevation measured on the curve.
            ///     See <see cref="TunnelRuns.Stored" />.
            /// </summary>
            /// <param name="entity">The node or edge that takes the elevation.</param>
            /// <param name="bezier">The curve to measure on.</param>
            /// <param name="t">Curve position to measure at.</param>
            /// <param name="geometry">The geometry data of the edge's prefab.</param>
            private void OutputUnderground(
                Entity          entity,
                Bezier4x3       bezier,
                float           t,
                NetGeometryData geometry) {
                var terrain   = TerrainHeight;
                var limit     = geometry.m_ElevationLimit;
                var half      = geometry.m_DefaultWidth * 0.5f;
                var measured  = TunnelRuns.Elevation(ref terrain, bezier, t, half);
                var had       = ElevationLookup.TryGetComponent(entity, out var existing);
                var elevation = TunnelRuns.Stored(measured, existing.m_Elevation, limit);

                // The game keeps an elevation of zero only on a building's own network.
                if (math.any(elevation != 0f) || (had && OwnerLookup.HasComponent(entity))) {
                    ECB.AddComponent(entity, new Elevation(elevation));
                } else if (had) {
                    ECB.RemoveComponent<Elevation>(entity);
                }
            }

            /// <summary>
            ///     Updates a node's position and adjusts connected edges not in the selection.
            /// </summary>
            private void UpdateNodeAndConnectedEdges(
                Entity                 nodeEntity,
                float3                 newPosition,
                NativeArray<EdgeState> selectedEdges,
                NativeArray<NodeState> selectedNodes) {
                // Update node position
                ECB.SetComponent(nodeEntity, new Node { m_Position = newPosition });
                MarkNodeUpdated(nodeEntity);

                if (!HasNodePositionChanged(nodeEntity, newPosition)) {
                    return;
                }

                if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
                    return;
                }

                for (var i = 0; i < connectedEdges.Length; i++) {
                    var connectedEdgeEntity = connectedEdges[i].m_Edge;

                    if (IsEdgeInSelection(connectedEdgeEntity, selectedEdges)) {
                        continue;
                    }

                    AdjustConnectedEdgeAtNode(connectedEdgeEntity, nodeEntity, selectedNodes);
                }
            }

            /// <summary>
            ///     Adjusts a connected edge's bezier control points at the intersection node.
            ///     Applies the node movement delta to preserve the original offset between
            ///     node center and bezier endpoint.
            ///     An edge joining two moved nodes follows both.
            /// </summary>
            private void AdjustConnectedEdgeAtNode(
                Entity                 edgeEntity,
                Entity                 nodeEntity,
                NativeArray<NodeState> selectedNodes) {
                if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                    return;
                }

                if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                    return;
                }

                if (edge.m_Start != nodeEntity && edge.m_End != nodeEntity) {
                    return;
                }

                var bezier     = curve.m_Bezier;
                var startDelta = NodeDelta(edge.m_Start, selectedNodes);
                var endDelta   = NodeDelta(edge.m_End, selectedNodes);

                // Shift each endpoint and control point by its node's movement delta.
                bezier.a += startDelta;
                bezier.b += startDelta;
                bezier.d += endDelta;
                bezier.c += endDelta;

                ECB.SetComponent(edgeEntity,
                                 new Curve {
                                     m_Bezier = bezier,
                                     m_Length = MathUtils.Length(bezier)
                                 });
                MarkUpdated(edgeEntity);
            }

            /// <summary>
            ///     Gets how far a selected node moves, or zero for a node outside the selection.
            /// </summary>
            /// <param name="nodeEntity">The node.</param>
            /// <param name="selectedNodes">Node states of the path.</param>
            /// <returns>The node's movement delta.</returns>
            private static float3 NodeDelta(
                Entity                 nodeEntity,
                NativeArray<NodeState> selectedNodes) {
                for (var i = 0; i < selectedNodes.Length; i++) {
                    if (selectedNodes[i].Entity == nodeEntity) {
                        return selectedNodes[i].Position - selectedNodes[i].OriginalPosition;
                    }
                }

                return float3.zero;
            }

            /// <summary>
            ///     Checks if an edge entity is in the selection.
            /// </summary>
            private static bool IsEdgeInSelection(Entity edgeEntity, NativeArray<EdgeState> selectedEdges) {
                for (var i = 0; i < selectedEdges.Length; i++) {
                    if (selectedEdges[i].EdgeEntity == edgeEntity) {
                        return true;
                    }
                }

                return false;
            }


            /// <summary>
            ///     Marks an entity as updated with Updated and BatchesUpdated components.
            /// </summary>
            private void MarkUpdated(Entity entity) {
                ECB.AddComponent<Updated>(entity);
                ECB.AddComponent<BatchesUpdated>(entity);
            }

            /// <summary>
            ///     Marks a node and all its connected edges as updated.
            /// </summary>
            private void MarkNodeUpdated(Entity nodeEntity) {
                MarkUpdated(nodeEntity);

                if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
                    return;
                }

                for (var i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        continue;
                    }

                    if (edge.m_Start != nodeEntity && edge.m_End != nodeEntity) {
                        continue;
                    }

                    MarkUpdated(edgeEntity);

                    if (edge.m_Start != nodeEntity) {
                        MarkUpdated(edge.m_Start);
                    } else if (edge.m_End != nodeEntity) {
                        MarkUpdated(edge.m_End);
                    }

                    if (AggregatedLookup.TryGetComponent(edgeEntity, out var aggregated)) {
                        MarkUpdated(aggregated.m_Aggregate);
                    }
                }
            }
        }
    }
}