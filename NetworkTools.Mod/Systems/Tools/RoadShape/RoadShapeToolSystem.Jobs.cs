namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_RoadShapeToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct ShapeTransformJob : IJob {
            [ReadOnly] public required NativeList<EdgeState>             EdgeStates;
            [ReadOnly] public required ShapeTransformContext             Context;
            [ReadOnly] public required ShapeTransformConfig              Config;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
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

                // 2. Execute transformation (context = path geometry, config = user settings)
                switch (Config.Template) {
                    case ShapeTransformTemplate.SlopeLinear:
                        var linearTransform = new SlopeLinearTransform();
                        TransformPipeline.Execute(ref linearTransform, ref edges, in Context, in Config);
                        break;
                    case ShapeTransformTemplate.SlopeEaseInOut:
                        var easeInOutTransform = new SlopeEaseInOutTransform();
                        TransformPipeline.Execute(ref easeInOutTransform, ref edges, in Context, in Config);
                        break;
                    case ShapeTransformTemplate.SlopeArch:
                        // TODO: Implement SlopeParabolicTransform
                        break;
                    case ShapeTransformTemplate.CurveStraighten:
                        var straightenTransform = new CurveStraightenTransform();
                        TransformPipeline.Execute(ref straightenTransform, ref edges, in Context, in Config);
                        break;
                    case ShapeTransformTemplate.CurveSmooth:
                        var smoothTransform = new CurveSmoothTransform();
                        TransformPipeline.Execute(ref smoothTransform, ref edges, in Context, in Config);
                        break;
                }

                // 3. Output
                Output(edges, in Context);

                // Cleanup
                edges.Dispose();
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

            private void Output(
                NativeArray<EdgeState>   edges,
                in ShapeTransformContext ctx) {
                if (OutputMode == ToolOutputMode.Preview) {
                    OutputPreview(edges);
                } else {
                    OutputApply(edges);
                }
            }

            /// <summary>
            ///     Collects the new position for each node after transformation.
            /// </summary>
            private static NativeHashMap<Entity, float3> CollectNodePositions(NativeArray<EdgeState> edges) {
                var nodePositions = new NativeHashMap<Entity, float3>(edges.Length * 2, Allocator.Temp);
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    nodePositions.TryAdd(state.StartNode, state.Bezier.a);
                    nodePositions.TryAdd(state.EndNode, state.Bezier.d);
                }

                return nodePositions;
            }

            /// <summary>
            ///     Gets the height delta for a node, or zero if the node doesn't exist or delta is negligible.
            /// </summary>
            private float GetNodeHeightDelta(Entity nodeEntity, float3 newPosition) {
                if (!NodeLookup.TryGetComponent(nodeEntity, out var node)) {
                    return 0f;
                }

                var delta = newPosition.y - node.m_Position.y;
                return math.abs(delta) < HeightDeltaThreshold ? 0f : delta;
            }

            /// <summary>
            ///     Creates CreationDefinition + NetCourse entities for preview.
            /// </summary>
            private void OutputPreview(NativeArray<EdgeState> edges) {
                var processedNodes = new NativeHashSet<Entity>(edges.Length * 2, Allocator.Temp);
                var nodePositions  = CollectNodePositions(edges);

                // Output selected edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    OutputPreviewEdge(state.EdgeEntity,
                                      state.Bezier,
                                      MathUtils.Length(state.Bezier),
                                      state.NetworkComposition,
                                      Entity.Null,
                                      Entity.Null);
                }

                // For now, disable previewing connected edges until we fix the temp connection bug.

                //// Output connected edges at each node
                //for (var i = 0; i < edges.Length; i++) {
                //    var state = edges[i];

                //    if (processedNodes.Add(state.StartNode)) {
                //        PreviewConnectedEdges(state.StartNode, nodePositions[state.StartNode], edges);
                //    }

                //    if (processedNodes.Add(state.EndNode)) {
                //        PreviewConnectedEdges(state.EndNode, nodePositions[state.EndNode], edges);
                //    }
                //}

                processedNodes.Dispose();
                nodePositions.Dispose();
            }

            /// <summary>
            ///     Creates preview entities for edges connected to a node that are not in the selection.
            /// </summary>
            private void PreviewConnectedEdges(
                Entity                 nodeEntity,
                float3                 newPosition,
                NativeArray<EdgeState> selectedEdges) {
                var heightDelta = GetNodeHeightDelta(nodeEntity, newPosition);
                if (heightDelta == 0f) {
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

                    OutputPreviewConnectedEdge(connectedEdgeEntity, nodeEntity, heightDelta);
                }
            }

            /// <summary>
            ///     Creates a preview entity for a connected edge with adjusted control points at the intersection.
            /// </summary>
            private void OutputPreviewConnectedEdge(Entity edgeEntity, Entity nodeEntity, float heightDelta) {
                if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                    return;
                }

                if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                    return;
                }

                var    bezier = curve.m_Bezier;
                Entity startNodeRef;
                Entity endNodeRef;

                if (edge.m_Start == nodeEntity) {
                    bezier.a.y   += heightDelta;
                    bezier.b.y   += heightDelta;
                    startNodeRef =  Entity.Null;
                    endNodeRef   =  edge.m_End;
                } else if (edge.m_End == nodeEntity) {
                    bezier.d.y   += heightDelta;
                    bezier.c.y   += heightDelta;
                    startNodeRef =  edge.m_Start;
                    endNodeRef   =  Entity.Null;
                } else {
                    return;
                }

                var composition = GetNetworkComposition(edgeEntity);
                OutputPreviewEdge(edgeEntity,
                                  bezier,
                                  MathUtils.Length(bezier),
                                  composition,
                                  startNodeRef,
                                  endNodeRef);
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
                Entity             endNodeEntity) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = edgeEntity,
                    m_Flags    = CreationFlags.Recreate | CreationFlags.Parent
                };

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

                // Initialize elevations from bezier heights
                var startElevation = new float2(bezier.a.y, bezier.a.y);
                var endElevation = new float2(bezier.d.y, bezier.d.y);
                var courseElevation = new float2(bezier.a.y, bezier.d.y);

                var netCourse = new NetCourse {
                    m_Curve      = bezier,
                    m_Length     = length,
                    m_FixedIndex = -1,
                    m_Elevation  = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = startNodeEntity,
                        m_Position      = bezier.a,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = endNodeEntity,
                        m_Position      = bezier.d,
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
            private void OutputApply(NativeArray<EdgeState> edges) {
                var processedNodes = new NativeHashSet<Entity>(edges.Length * 2, Allocator.Temp);
                var nodePositions  = CollectNodePositions(edges);

                // Apply curve changes to selected edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    ECB.SetComponent(state.EdgeEntity,
                                     new Curve {
                                         m_Bezier = state.Bezier,
                                         m_Length = MathUtils.Length(state.Bezier)
                                     });
                }

                // Update nodes and connected edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];

                    if (processedNodes.Add(state.StartNode)) {
                        UpdateNodeAndConnectedEdges(state.StartNode, nodePositions[state.StartNode], edges);
                    }

                    if (processedNodes.Add(state.EndNode)) {
                        UpdateNodeAndConnectedEdges(state.EndNode, nodePositions[state.EndNode], edges);
                    }
                }

                processedNodes.Dispose();
                nodePositions.Dispose();
            }

            /// <summary>
            ///     Updates a node's position and adjusts connected edges not in the selection.
            /// </summary>
            private void UpdateNodeAndConnectedEdges(
                Entity                 nodeEntity,
                float3                 newPosition,
                NativeArray<EdgeState> selectedEdges) {
                // Update node position
                ECB.SetComponent(nodeEntity, new Node { m_Position = newPosition });
                MarkNodeUpdated(nodeEntity);

                var heightDelta = GetNodeHeightDelta(nodeEntity, newPosition);
                if (heightDelta == 0f) {
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

                    AdjustConnectedEdgeAtNode(connectedEdgeEntity, nodeEntity, heightDelta);
                }
            }

            /// <summary>
            ///     Adjusts a connected edge's bezier control points at the intersection node.
            /// </summary>
            private void AdjustConnectedEdgeAtNode(Entity edgeEntity, Entity nodeEntity, float heightDelta) {
                if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                    return;
                }

                if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                    return;
                }

                var bezier = curve.m_Bezier;

                // Adjust the endpoint and control point at the intersection
                if (edge.m_Start == nodeEntity) {
                    bezier.a.y += heightDelta;
                    bezier.b.y += heightDelta;
                } else if (edge.m_End == nodeEntity) {
                    bezier.d.y += heightDelta;
                    bezier.c.y += heightDelta;
                } else {
                    return;
                }

                ECB.SetComponent(edgeEntity,
                                 new Curve {
                                     m_Bezier = bezier,
                                     m_Length = MathUtils.Length(bezier)
                                 });
                MarkUpdated(edgeEntity);
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