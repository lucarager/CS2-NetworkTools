// <copyright file="NT_PathTransformToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_PathTransformToolSystem {
        /// <summary>
        ///     Minimum height delta (in meters) to consider for intersection adjustments.
        /// </summary>
        private const float HeightDeltaThreshold = 0.001f;

        /// <summary>
        ///     Minimum XZ delta squared (in meters²) to consider for intersection adjustments.
        /// </summary>
        private const float XZDeltaSquaredThreshold = 0.000001f;

        public const float TunnelThreshold = -12f;
        public const float ElevatedThreshold = 8f;
        public const float ForceGroundElevation = 0f;

#if BURST
        [BurstCompile]
#endif
        /// <summary>
        ///     Unified job for path transformations. Computes adjusted beziers and outputs
        ///     either preview definitions or applies changes to existing entities.
        ///     Supports both shape (XZ) and slope (Y) transformations.
        ///     Pipeline:
        ///     1. Initialize context (path-level data)
        ///     2. Gather edge states (per-edge data, single loop)
        ///     3. Apply shape transforms (XZ modifications)
        ///     4. Apply slope transforms (Y modifications)
        ///     5. Output results (preview or apply)
        /// </summary>
        internal struct PathTransformJob : IJob {
            [ReadOnly] public required NativeList<Entity> SelectedNodes;
            [ReadOnly] public required NativeList<Entity> CurrentPathEdges;
            [ReadOnly] public required NativeList<Entity> CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<Upgraded> UpgradedLookup;
            [ReadOnly] public required TransformConfig Config;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Aggregated> AggregatedLookup;
            public required TransformOutputMode OutputMode;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                // === 1. Initialize context ===
                var context = InitializeContext();

                // === 2. Gather edge states ===
                var edges = GatherEdgeStates(ref context);
                if (edges.Length == 0) {
                    edges.Dispose();
                    return;
                }

                // === 3. Apply shape transforms (XZ) ===
                PathTransformUtility.ApplyShapeTransforms(edges, in context);

                // === 3b. Recalculate geometry after shape transforms ===
                if (context.Config.HasShapeTransform) {
                    PathTransformUtility.RecalculateGeometry(edges, ref context);
                }

                // === 4. Apply slope transforms (Y) ===
                PathTransformUtility.ApplySlopeTransforms(edges, in context);

                // === 5. Gather intersection adjustments ===
                var intersectionAdjustments = GatherIntersectionAdjustments(edges, in context);

                // === 6. Output results ===
                Output(edges, intersectionAdjustments, in context);

                intersectionAdjustments.Dispose();
                edges.Dispose();
            }


            /// <summary>
            ///     Creates the path-level context from endpoint node positions.
            /// </summary>
            private TransformContext InitializeContext() {
                var startNode = SelectedNodes[0];
                var endNode = SelectedNodes[^1];
                var startPos = NodeLookup[startNode].m_Position;
                var endPos = NodeLookup[endNode].m_Position;

                return TransformContext.Create(startPos, endPos, Config);
            }

            /// <summary>
            ///     Gathers all edge data in a single loop, calculating cumulative distances
            ///     and total path length. Updates context.TotalLength.
            /// </summary>
            private NativeArray<EdgeTransformState> GatherEdgeStates(ref TransformContext context) {
                var edgeCount = CurrentPathEdges.Length;
                var edges = new NativeArray<EdgeTransformState>(edgeCount, Allocator.Temp);
                var cumulativeDistance = 0f;

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var state = new EdgeTransformState {
                        EdgeEntity         = edgeEntity,
                        PathIndex          = i,
                        CumulativeDistance = cumulativeDistance
                    };

                    // Get edge component for direction and node references
                    if (EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        state.StartNode = edge.m_Start;
                        state.EndNode   = edge.m_End;

                        var currentNode = CurrentPathNodes[i];
                        state.IsForward = edge.m_Start == currentNode;
                    }

                    state.NetworkComposition = GetNetworkComposition(edgeEntity);

                    // Get curve component for geometry
                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        state.Bezier = curve.m_Bezier;
                        state.CalculateLength();
                        state.RecalculateControlPointRatios();
                    }

                    // Store original values for intersection delta calculations
                    var pathEndNode = CurrentPathNodes[i + 1];
                    if (NodeLookup.TryGetComponent(pathEndNode, out var pathEndNodeInfo)) {
                        state.OriginalEndHeight = pathEndNodeInfo.m_Position.y;
                        state.OriginalEndXZ = new float2(pathEndNodeInfo.m_Position.x, pathEndNodeInfo.m_Position.z);
                    }

                    edges[i]           =  state;
                    cumulativeDistance += state.Length;
                }

                // Update context with total length
                context.TotalLength = cumulativeDistance;

                return edges;
            }

            /// <summary>
            ///     Outputs the transformed edges as either preview entities or applied changes.
            /// </summary>
            private void Output(
                NativeArray<EdgeTransformState> edges,
                NativeList<IntersectionEdgeAdjustment> intersectionAdjustments,
                in TransformContext ctx) {
                if (OutputMode == TransformOutputMode.Preview) {
                    OutputPreview(edges, intersectionAdjustments);
                }
                else {
                    OutputApply(edges, intersectionAdjustments);
                }
            }

            /// <summary>
            ///     Creates CreationDefinition + NetCourse entities for preview.
            /// </summary>
            private void OutputPreview(
                NativeArray<EdgeTransformState> edges,
                NativeList<IntersectionEdgeAdjustment> intersectionAdjustments) {
                // Output path edges (both endpoints free)
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    OutputPreviewEdge(
                        state.EdgeEntity,
                        state.Bezier,
                        MathUtils.Length(state.Bezier),
                        state.NetworkComposition,
                        Entity.Null,
                        Entity.Null);
                }

                // Output intersection edge adjustments (far node fixed, path node free)
                for (var i = 0; i < intersectionAdjustments.Length; i++) {
                    var adj = intersectionAdjustments[i];
                    OutputPreviewEdge(
                        adj.EdgeEntity,
                        adj.Bezier,
                        adj.Length,
                        adj.NetworkComposition,
                        adj.PathNodeIsStart ? Entity.Null : adj.FarNode,
                        adj.PathNodeIsStart ? adj.FarNode : Entity.Null);
                }
            }

            /// <summary>
            ///     Creates a preview entity for an edge with configurable node references.
            /// </summary>
            private void OutputPreviewEdge(
                Entity edgeEntity,
                Bezier4x3 bezier,
                float length,
                NetworkComposition composition,
                Entity startNodeEntity,
                Entity endNodeEntity) {
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

                var elevation = GetElevationFromComposition(composition);
                var compositionFlags = GetFlagsFromComposition(composition);

                var startNodeFlags = compositionFlags;
                var endNodeFlags = compositionFlags;

                if (startNodeEntity != Entity.Null && endNodeEntity == Entity.Null) {
                    startNodeFlags |= CoursePosFlags.IsFirst;
                } else if (endNodeEntity != Entity.Null && startNodeEntity == Entity.Null) {
                    endNodeFlags |= CoursePosFlags.IsFirst;
                }

                var netCourse = new NetCourse {
                    m_Curve      = bezier,
                    m_Length     = length,
                    m_FixedIndex = -1,
                    m_Elevation  = elevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = startNodeEntity,
                        m_Position      = bezier.a,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = elevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = endNodeEntity,
                        m_Position      = bezier.d,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier)),
                        m_CourseDelta   = 1,
                        m_Elevation     = elevation,
                        m_Flags         = endNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }

            /// <summary>
            ///     Gets the elevation value for a network composition.
            /// </summary>
            private static float2 GetElevationFromComposition(NetworkComposition composition) {
                return composition switch {
                    NetworkComposition.Elevated => ElevatedThreshold,
                    NetworkComposition.Tunnel => TunnelThreshold,
                    NetworkComposition.Ground => ForceGroundElevation,
                    _ => float2.zero
                };
            }

            /// <summary>
            ///     Gets the flags for a network composition.
            /// </summary>
            private static CoursePosFlags GetFlagsFromComposition(NetworkComposition composition) {
                return composition switch {
                    NetworkComposition.Elevated => CoursePosFlags.ForceElevatedEdge | CoursePosFlags.ForceElevatedNode,
                    NetworkComposition.Tunnel => 0,
                    NetworkComposition.Ground => 0,
                    _ => 0
                };
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
            ///     Applies transformation changes to existing Curve components and intersection adjustments.
            /// </summary>
            private void OutputApply(
                NativeArray<EdgeTransformState> edges,
                NativeList<IntersectionEdgeAdjustment> intersectionAdjustments) {
                // Apply curve changes to path edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];

                    var curve = new Curve {
                        m_Bezier = state.Bezier,
                        m_Length = MathUtils.Length(state.Bezier)
                    };
                    ECB.SetComponent(state.EdgeEntity, curve);

                    MarkNodeUpdated(state.StartNode);
                    MarkNodeUpdated(state.EndNode);
                }

                // Apply intersection adjustments
                for (var i = 0; i < intersectionAdjustments.Length; i++) {
                    var adjustment = intersectionAdjustments[i];

                    var curve = new Curve {
                        m_Bezier = adjustment.Bezier,
                        m_Length = adjustment.Length
                    };
                    ECB.SetComponent(adjustment.EdgeEntity, curve);

                    MarkUpdated(adjustment.EdgeEntity);
                    MarkUpdated(adjustment.PathNode);
                    MarkUpdated(adjustment.FarNode);
                }
            }

            /// <summary>
            ///     Gathers all intersection edge adjustments for non-path edges connected to intersection nodes.
            /// </summary>
            private NativeList<IntersectionEdgeAdjustment> GatherIntersectionAdjustments(
                NativeArray<EdgeTransformState> edges,
                in TransformContext ctx) {
                var adjustments = new NativeList<IntersectionEdgeAdjustment>(Allocator.Temp);

                // Build path edge set for filtering
                var pathEdgeSet = new NativeHashSet<Entity>(edges.Length, Allocator.Temp);
                for (var i = 0; i < edges.Length; i++) {
                    pathEdgeSet.Add(edges[i].EdgeEntity);
                }

                var firstNodePos = NodeLookup[CurrentPathNodes[0]].m_Position;
                var firstNodeOldHeight = firstNodePos.y;
                var firstNodeOldXZ = new float2(firstNodePos.x, firstNodePos.z);

                for (var i = 0; i < CurrentPathNodes.Length; i++) {
                    var nodeEntity = CurrentPathNodes[i];

                    if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
                        continue;
                    }

                    // Only process intersection nodes (more than 2 connected edges)
                    if (connectedEdges.Length <= 2) {
                        continue;
                    }

                    // Get cumulative distance at this node
                    var cumulativeDistance = i == 0 ? 0f : PathTransformUtility.GetCumulativeDistanceAtNode(edges, i);

                    // Calculate height delta for this node
                    var heightDelta = 0f;
                    if (ctx.Config.HasSlopeTransform) {
                        var oldHeight = i == 0 ? firstNodeOldHeight : edges[i - 1].OriginalEndHeight;
                        var newHeight = SlopeCalculator.CalculateHeight(cumulativeDistance,
                            ctx.TotalLength,
                            ctx.StartHeight,
                            ctx.DeltaHeight,
                            ctx.Config.Slope);
                        heightDelta = newHeight - oldHeight;
                    }

                    // Calculate XZ delta for this node
                    var xzDelta = float2.zero;
                    if (ctx.Config.HasShapeTransform && ctx.Config.Shape.Template == ShapeTemplate.Straighten) {
                        var oldXZ = i == 0 ? firstNodeOldXZ : edges[i - 1].OriginalEndXZ;
                        var newXZ = ShapeCalculator.CalculatePositionLinear(cumulativeDistance,
                            ctx.TotalLength,
                            ctx.StartXZ,
                            ctx.EndXZ);
                        xzDelta = newXZ - oldXZ;
                    }

                    var hasHeightDelta = math.abs(heightDelta) >= HeightDeltaThreshold;
                    var hasXZDelta = math.lengthsq(xzDelta) >= XZDeltaSquaredThreshold;

                    if (!hasHeightDelta && !hasXZDelta) {
                        continue;
                    }

                    // Gather adjustments for connected edges that are not part of the path
                    GatherConnectedEdgeAdjustments(
                        nodeEntity,
                        connectedEdges,
                        pathEdgeSet,
                        heightDelta,
                        xzDelta,
                        adjustments);
                }

                pathEdgeSet.Dispose();
                return adjustments;
            }

            /// <summary>
            ///     Gathers adjustments for edges connected to an intersection node that are not part of the path.
            /// </summary>
            private void GatherConnectedEdgeAdjustments(
                Entity nodeEntity,
                DynamicBuffer<ConnectedEdge> connectedEdges,
                NativeHashSet<Entity> pathEdgeSet,
                float heightDelta,
                float2 xzDelta,
                NativeList<IntersectionEdgeAdjustment> adjustments) {
                for (var j = 0; j < connectedEdges.Length; j++) {
                    var connectedEdgeEntity = connectedEdges[j].m_Edge;

                    if (pathEdgeSet.Contains(connectedEdgeEntity)) {
                        continue;
                    }

                    if (!EdgeLookup.TryGetComponent(connectedEdgeEntity, out var connectedEdge)) {
                        continue;
                    }

                    if (!CurveLookup.TryGetComponent(connectedEdgeEntity, out var curve)) {
                        continue;
                    }

                    var bezier = curve.m_Bezier;
                    var pathNodeIsStart = connectedEdge.m_Start == nodeEntity;

                    // Apply delta to endpoint and adjacent control point
                    if (pathNodeIsStart) {
                        bezier.a.y += heightDelta;
                        bezier.b.y += heightDelta;
                        bezier.a.x += xzDelta.x;
                        bezier.a.z += xzDelta.y;
                        bezier.b.x += xzDelta.x;
                        bezier.b.z += xzDelta.y;
                    }
                    else {
                        bezier.d.y += heightDelta;
                        bezier.c.y += heightDelta;
                        bezier.d.x += xzDelta.x;
                        bezier.d.z += xzDelta.y;
                        bezier.c.x += xzDelta.x;
                        bezier.c.z += xzDelta.y;
                    }

                    adjustments.Add(new IntersectionEdgeAdjustment {
                        EdgeEntity         = connectedEdgeEntity,
                        Bezier             = bezier,
                        Length             = curve.m_Length,
                        PathNode           = nodeEntity,
                        FarNode            = pathNodeIsStart ? connectedEdge.m_End : connectedEdge.m_Start,
                        PathNodeIsStart    = pathNodeIsStart,
                        NetworkComposition = GetNetworkComposition(connectedEdgeEntity)
                    });
                }
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
                    }
                    else if (edge.m_End != nodeEntity) {
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