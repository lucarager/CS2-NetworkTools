// <copyright file="NT_SlopeToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_SlopeToolSystem {
#if BURST
        [BurstCompile]
#endif
        /// <summary>
        /// Unified job for slope transformations. Computes adjusted beziers and outputs
        /// either preview definitions or applies changes to existing entities.
        /// </summary>
        private struct SlopeTransformJob : IJob {
            [ReadOnly] public required NativeList<Entity>                SelectedNodes;
            [ReadOnly] public required NativeList<Entity>                CurrentPathEdges;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required SlopeCurveConfig                  CurveConfig;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
            public required SlopeOutputMode   OutputMode;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                var startNode     = SelectedNodes[0];
                var endNode       = SelectedNodes[^1];
                var startNodeInfo = NodeLookup[startNode];
                var endNodeInfo   = NodeLookup[endNode];
                var startHeight   = startNodeInfo.m_Position.y;
                var endHeight     = endNodeInfo.m_Position.y;
                var deltaHeight   = endHeight - startHeight;

                // === Phase 1: Gather edge metadata and calculate total path length ===
                var edgeCount   = CurrentPathEdges.Length;
                var edgeData    = new NativeArray<EdgeSlopeData>(edgeCount, Allocator.Temp);
                var totalLength = 0f;

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var data       = new EdgeSlopeData();

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        edgeData[i] = data;
                        continue;
                    }

                    var currentNode = CurrentPathNodes[i];
                    data.IsForward = edge.m_Start == currentNode;

                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        data.Length = curve.m_Length;

                        SlopeCalculator.CalculateControlPointRatios(
                            curve.m_Bezier,
                            data.Length,
                            data.IsForward,
                            out data.CtrlStartRatio,
                            out data.CtrlEndRatio);
                    }

                    // Store old height at path-end node for intersection updates (Apply mode)
                    var pathEndNode = CurrentPathNodes[i + 1];
                    if (NodeLookup.TryGetComponent(pathEndNode, out var pathEndNodeInfo)) {
                        data.OldHeight = pathEndNodeInfo.m_Position.y;
                    }

                    edgeData[i] = data;
                    totalLength += data.Length;
                }

                if (totalLength <= 0f) {
                    edgeData.Dispose();
                    return;
                }

                // === Phase 2: Calculate heights and build computed edges ===
                var computedEdges = new NativeList<ComputedEdgeSlope>(edgeCount, Allocator.Temp);
                var cumulativeDistance = 0f;

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var data       = edgeData[i];

                    if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        cumulativeDistance += data.Length;
                        continue;
                    }

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        cumulativeDistance += data.Length;
                        continue;
                    }

                    var heights = SlopeCalculator.CalculateEdgeHeights(
                        cumulativeDistance,
                        data.Length,
                        data.CtrlStartRatio,
                        data.CtrlEndRatio,
                        totalLength,
                        startHeight,
                        deltaHeight,
                        CurveConfig);

                    var adjustedBezier = SlopeCalculator.ApplyHeightsToBezier(curve.m_Bezier, heights, data.IsForward);

                    computedEdges.Add(new ComputedEdgeSlope {
                        PathIndex          = i,
                        EdgeEntity         = edgeEntity,
                        StartNode          = edge.m_Start,
                        EndNode            = edge.m_End,
                        AdjustedBezier     = adjustedBezier,
                        CumulativeDistance = cumulativeDistance,
                        Metadata           = data,
                    });

                    cumulativeDistance += data.Length;
                }

                if (computedEdges.Length == 0) {
                    edgeData.Dispose();
                    computedEdges.Dispose();
                    return;
                }

                // === Output Phase ===
                if (OutputMode == SlopeOutputMode.Preview) {
                    OutputPreview(computedEdges);
                } else {
                    OutputApply(computedEdges, edgeData, totalLength, startHeight, deltaHeight);
                }

                edgeData.Dispose();
                computedEdges.Dispose();
            }

            /// <summary>
            /// Creates CreationDefinition + NetCourse entities for preview.
            /// </summary>
            private void OutputPreview(NativeList<ComputedEdgeSlope> computedEdges) {
                for (var i = 0; i < computedEdges.Length; i++) {
                    var computed = computedEdges[i];
                    var definitionEntity = ECB.CreateEntity();

                    var creationDefinition = new CreationDefinition {
                        m_Original = computed.EdgeEntity,
                        m_Flags    = CreationFlags.Recreate | CreationFlags.Parent,
                    };

                    if (PrefabRefLookup.TryGetComponent(computed.EdgeEntity, out var prefabRef)) {
                        creationDefinition.m_Prefab = prefabRef;
                    }

                    if (PseudoRandomSeedLookup.TryGetComponent(computed.EdgeEntity, out var seed)) {
                        creationDefinition.m_RandomSeed = seed.m_Seed;
                    }

                    ECB.AddComponent(definitionEntity, creationDefinition);
                    ECB.AddComponent<Updated>(definitionEntity);

                    var netCourse = new NetCourse {
                        m_Curve      = computed.AdjustedBezier,
                        m_Length     = MathUtils.Length(computed.AdjustedBezier),
                        m_FixedIndex = -1,
                        m_Elevation  = default,
                        m_StartPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = computed.AdjustedBezier.a,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(computed.AdjustedBezier)),
                            m_CourseDelta   = 0,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                        m_EndPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = computed.AdjustedBezier.d,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(computed.AdjustedBezier)),
                            m_CourseDelta   = 1,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                    };

                    ECB.AddComponent(definitionEntity, netCourse);
                }
            }

            /// <summary>
            /// Applies slope changes to existing Curve components and handles intersection adjustments.
            /// </summary>
            private void OutputApply(
                NativeList<ComputedEdgeSlope> computedEdges,
                NativeArray<EdgeSlopeData> edgeData,
                float totalLength,
                float startHeight,
                float deltaHeight) {
                // Build path edge set for intersection filtering
                var pathEdgeSet = new NativeHashSet<Entity>(computedEdges.Length, Allocator.Temp);

                // Apply curve changes to path edges
                for (var i = 0; i < computedEdges.Length; i++) {
                    var computed = computedEdges[i];
                    pathEdgeSet.Add(computed.EdgeEntity);

                    var curve = new Curve { m_Bezier = computed.AdjustedBezier, m_Length = MathUtils.Length(computed.AdjustedBezier) };
                    ECB.SetComponent(computed.EdgeEntity, curve);

                    MarkNodeUpdated(computed.StartNode);
                    MarkNodeUpdated(computed.EndNode);
                }

                // === Phase 3: Handle intersection adjustments ===
                HandleIntersections(edgeData, pathEdgeSet, totalLength, startHeight, deltaHeight);

                pathEdgeSet.Dispose();
            }

            /// <summary>
            /// Adjusts non-path edges connected to intersection nodes to preserve their original slopes.
            /// </summary>
            private void HandleIntersections(
                NativeArray<EdgeSlopeData> edgeData,
                NativeHashSet<Entity> pathEdgeSet,
                float totalLength,
                float startHeight,
                float deltaHeight) {
                // First node uses startHeight as both old and new
                var firstNodeOldHeight = NodeLookup[CurrentPathNodes[0]].m_Position.y;
                var edgeCount          = edgeData.Length;
                var cumulativeDistance = 0f;

                for (var i = 0; i < CurrentPathNodes.Length; i++) {
                    var nodeEntity = CurrentPathNodes[i];

                    if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
                        if (i < edgeCount) cumulativeDistance += edgeData[i].Length;
                        continue;
                    }

                    // Only process intersection nodes (more than 2 connected edges)
                    if (connectedEdges.Length <= 2) {
                        if (i < edgeCount) cumulativeDistance += edgeData[i].Length;
                        continue;
                    }

                    // Calculate height delta for this node
                    float oldHeight, newHeight;
                    if (i == 0) {
                        oldHeight = firstNodeOldHeight;
                        newHeight = startHeight;
                    } else {
                        oldHeight = edgeData[i - 1].OldHeight;
                        newHeight = SlopeCalculator.CalculateHeight(cumulativeDistance, totalLength, startHeight, deltaHeight, CurveConfig);
                    }

                    var heightDelta = newHeight - oldHeight;

                    if (math.abs(heightDelta) < 0.001f) {
                        if (i < edgeCount) cumulativeDistance += edgeData[i].Length;
                        continue;
                    }

                    // Adjust connected edges that are not part of the path
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

                        // Adjust endpoint and adjacent control point to preserve original slope
                        if (connectedEdge.m_Start == nodeEntity) {
                            bezier.a.y += heightDelta;
                            bezier.b.y += heightDelta;
                        } else if (connectedEdge.m_End == nodeEntity) {
                            bezier.d.y += heightDelta;
                            bezier.c.y += heightDelta;
                        }

                        var updatedCurve = new Curve { m_Bezier = bezier, m_Length = curve.m_Length };
                        ECB.SetComponent(connectedEdgeEntity, updatedCurve);

                        MarkUpdated(connectedEdgeEntity);
                        MarkUpdated(connectedEdge.m_Start);
                        MarkUpdated(connectedEdge.m_End);
                    }

                    if (i < edgeCount) cumulativeDistance += edgeData[i].Length;
                }
            }

            /// <summary>
            /// Marks an entity as updated with Updated and BatchesUpdated components.
            /// </summary>
            private void MarkUpdated(Entity entity) {
                ECB.AddComponent<Updated>(entity);
                ECB.AddComponent<BatchesUpdated>(entity);
            }

            /// <summary>
            /// Marks a node and all its connected edges as updated (replicates original Node_SetUpdated behavior).
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