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
        /// Unified job for path transformations. Computes adjusted beziers and outputs
        /// either preview definitions or applies changes to existing entities.
        /// Supports both shape (XZ) and slope (Y) transformations.
        /// 
        /// Pipeline:
        /// 1. Initialize context (path-level data)
        /// 2. Gather edge states (per-edge data, single loop)
        /// 3. Apply shape transforms (XZ modifications)
        /// 4. Apply slope transforms (Y modifications)
        /// 5. Output results (preview or apply)
        /// </summary>
        private struct PathTransformJob : IJob {
            [ReadOnly] public required NativeList<Entity>                SelectedNodes;
            [ReadOnly] public required NativeList<Entity>                CurrentPathEdges;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required TransformConfig                   Config;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
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
                ApplyShapeTransforms(edges, in context);

                // === 4. Apply slope transforms (Y) ===
                ApplySlopeTransforms(edges, in context);

                // === 5. Output results ===
                Output(edges, in context);

                edges.Dispose();
            }

            // ========================================
            // Pipeline Stage 1: Initialize Context
            // ========================================

            /// <summary>
            /// Creates the path-level context from endpoint node positions.
            /// </summary>
            private TransformContext InitializeContext() {
                var startNode = SelectedNodes[0];
                var endNode   = SelectedNodes[^1];
                var startPos  = NodeLookup[startNode].m_Position;
                var endPos    = NodeLookup[endNode].m_Position;

                return TransformContext.Create(startPos, endPos, Config);
            }

            // ========================================
            // Pipeline Stage 2: Gather Edge States
            // ========================================

            /// <summary>
            /// Gathers all edge data in a single loop, calculating cumulative distances
            /// and total path length. Updates context.TotalLength.
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
                        CumulativeDistance = cumulativeDistance,
                    };

                    // Get edge component for direction and node references
                    if (EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        state.StartNode = edge.m_Start;
                        state.EndNode   = edge.m_End;

                        var currentNode = CurrentPathNodes[i];
                        state.IsForward = edge.m_Start == currentNode;
                    }

                    // Get curve component for geometry
                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        state.Bezier = curve.m_Bezier;
                        state.Length = curve.m_Length;

                        SlopeCalculator.CalculateControlPointRatios(
                            curve.m_Bezier,
                            state.Length,
                            state.IsForward,
                            out state.CtrlStartRatio,
                            out state.CtrlEndRatio);
                    }

                    // Store original values for intersection delta calculations
                    var pathEndNode = CurrentPathNodes[i + 1];
                    if (NodeLookup.TryGetComponent(pathEndNode, out var pathEndNodeInfo)) {
                        state.OriginalEndHeight = pathEndNodeInfo.m_Position.y;
                        state.OriginalEndXZ = new float2(pathEndNodeInfo.m_Position.x, pathEndNodeInfo.m_Position.z);
                    }

                    edges[i] = state;
                    cumulativeDistance += state.Length;
                }

                // Update context with total length
                context.TotalLength = cumulativeDistance;

                return edges;
            }

            // ========================================
            // Pipeline Stage 3: Shape Transforms (XZ)
            // ========================================

            /// <summary>
            /// Applies shape transformations to all edges based on the configured template.
            /// </summary>
            private void ApplyShapeTransforms(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                if (!ctx.Config.HasShapeTransform) return;

                switch (ctx.Config.Shape.Template) {
                    case ShapeTemplate.Straighten:
                        ApplyStraightenTransform(edges, in ctx);
                        break;
                    case ShapeTemplate.Smooth:
                        ApplySmoothTransform(edges, in ctx);
                        break;
                }
            }

            /// <summary>
            /// Straightens all edges to lie on a direct line from path start to path end.
            /// </summary>
            private void ApplyStraightenTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];

                    var positions = ShapeCalculator.CalculateStraightenedPositions(
                        state.CumulativeDistance,
                        state.Length,
                        state.CtrlStartRatio,
                        state.CtrlEndRatio,
                        ctx.TotalLength,
                        ctx.StartXZ,
                        ctx.EndXZ);

                    state.Bezier = ShapeCalculator.ApplyPositionsToBezier(state.Bezier, positions, state.IsForward);
                    state.SetEvenControlPointRatios();

                    edges[i] = state;
                }
            }

            /// <summary>
            /// Smooths all edges to follow a master bezier curve from path start to path end.
            /// </summary>
            private void ApplySmoothTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                if (edges.Length == 0) return;

                // Calculate master bezier controls from first/last edge tangents
                var firstEdge = edges[0];
                var lastEdge  = edges[^1];

                var startTangent = ShapeCalculator.GetBezierTangentXZ(firstEdge.Bezier, true, firstEdge.IsForward);
                var endTangent   = ShapeCalculator.GetBezierTangentXZ(lastEdge.Bezier, false, lastEdge.IsForward);

                ShapeCalculator.CalculateMasterBezierControls(
                    ctx.StartXZ, ctx.EndXZ,
                    startTangent, endTangent,
                    ctx.TotalLength,
                    out var masterCtrl1, out var masterCtrl2);

                // Apply smooth transform to each edge
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];

                    var positions = ShapeCalculator.CalculateSmoothedPositions(
                        state.CumulativeDistance,
                        state.Length,
                        state.CtrlStartRatio,
                        state.CtrlEndRatio,
                        ctx.TotalLength,
                        ctx.StartXZ,
                        ctx.EndXZ,
                        masterCtrl1,
                        masterCtrl2,
                        ctx.Config.Shape.SmoothingFactor,
                        state.Bezier,
                        state.IsForward);

                    state.Bezier = ShapeCalculator.ApplyPositionsToBezier(state.Bezier, positions, state.IsForward);
                    state.RecalculateControlPointRatios();

                    edges[i] = state;
                }
            }

            // ========================================
            // Pipeline Stage 4: Slope Transforms (Y)
            // ========================================

            /// <summary>
            /// Applies slope transformations to all edges based on the configured template.
            /// </summary>
            private void ApplySlopeTransforms(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                if (!ctx.Config.HasSlopeTransform) return;

                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];

                    var heights = SlopeCalculator.CalculateEdgeHeights(
                        state.CumulativeDistance,
                        state.Length,
                        state.CtrlStartRatio,
                        state.CtrlEndRatio,
                        ctx.TotalLength,
                        ctx.StartHeight,
                        ctx.DeltaHeight,
                        ctx.Config.Slope);

                    state.Bezier = SlopeCalculator.ApplyHeightsToBezier(state.Bezier, heights, state.IsForward);

                    edges[i] = state;
                }
            }

            // ========================================
            // Pipeline Stage 5: Output
            // ========================================

            /// <summary>
            /// Outputs the transformed edges as either preview entities or applied changes.
            /// </summary>
            private void Output(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                if (OutputMode == TransformOutputMode.Preview) {
                    OutputPreview(edges);
                } else {
                    OutputApply(edges, ctx);
                }
            }

            /// <summary>
            /// Creates CreationDefinition + NetCourse entities for preview.
            /// </summary>
            private void OutputPreview(NativeArray<EdgeTransformState> edges) {
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    var definitionEntity = ECB.CreateEntity();

                    var creationDefinition = new CreationDefinition {
                        m_Original = state.EdgeEntity,
                        m_Flags    = CreationFlags.Recreate | CreationFlags.Parent,
                    };

                    if (PrefabRefLookup.TryGetComponent(state.EdgeEntity, out var prefabRef)) {
                        creationDefinition.m_Prefab = prefabRef;
                    }

                    if (PseudoRandomSeedLookup.TryGetComponent(state.EdgeEntity, out var seed)) {
                        creationDefinition.m_RandomSeed = seed.m_Seed;
                    }

                    ECB.AddComponent(definitionEntity, creationDefinition);
                    ECB.AddComponent<Updated>(definitionEntity);

                    var netCourse = new NetCourse {
                        m_Curve      = state.Bezier,
                        m_Length     = MathUtils.Length(state.Bezier),
                        m_FixedIndex = -1,
                        m_Elevation  = default,
                        m_StartPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = state.Bezier.a,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(state.Bezier)),
                            m_CourseDelta   = 0,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                        m_EndPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = state.Bezier.d,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(state.Bezier)),
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
            /// Applies transformation changes to existing Curve components and handles intersection adjustments.
            /// </summary>
            private void OutputApply(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
                // Build path edge set for intersection filtering
                var pathEdgeSet = new NativeHashSet<Entity>(edges.Length, Allocator.Temp);

                // Apply curve changes to path edges
                for (var i = 0; i < edges.Length; i++) {
                    var state = edges[i];
                    pathEdgeSet.Add(state.EdgeEntity);

                    var curve = new Curve {
                        m_Bezier = state.Bezier,
                        m_Length = MathUtils.Length(state.Bezier),
                    };
                    ECB.SetComponent(state.EdgeEntity, curve);

                    MarkNodeUpdated(state.StartNode);
                    MarkNodeUpdated(state.EndNode);
                }

                // Handle intersection adjustments
                HandleIntersections(edges, pathEdgeSet, ctx);

                pathEdgeSet.Dispose();
            }

            // ========================================
            // Intersection Handling
            // ========================================

            /// <summary>
            /// Adjusts non-path edges connected to intersection nodes to preserve their original slopes and positions.
            /// </summary>
            private void HandleIntersections(
                NativeArray<EdgeTransformState> edges,
                NativeHashSet<Entity> pathEdgeSet,
                in TransformContext ctx) {

                var firstNodePos       = NodeLookup[CurrentPathNodes[0]].m_Position;
                var firstNodeOldHeight = firstNodePos.y;
                var firstNodeOldXZ     = new float2(firstNodePos.x, firstNodePos.z);

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
                    var cumulativeDistance = (i == 0) ? 0f : GetCumulativeDistanceAtNode(edges, i);

                    // Calculate height delta for this node
                    float heightDelta = 0f;
                    if (ctx.Config.HasSlopeTransform) {
                        float oldHeight = (i == 0) ? firstNodeOldHeight : edges[i - 1].OriginalEndHeight;
                        float newHeight = SlopeCalculator.CalculateHeight(
                            cumulativeDistance, ctx.TotalLength, ctx.StartHeight, ctx.DeltaHeight, ctx.Config.Slope);
                        heightDelta = newHeight - oldHeight;
                    }

                    // Calculate XZ delta for this node
                    float2 xzDelta = float2.zero;
                    if (ctx.Config.HasShapeTransform && ctx.Config.Shape.Template == ShapeTemplate.Straighten) {
                        float2 oldXZ = (i == 0) ? firstNodeOldXZ : edges[i - 1].OriginalEndXZ;
                        float2 newXZ = ShapeCalculator.CalculatePositionLinear(
                            cumulativeDistance, ctx.TotalLength, ctx.StartXZ, ctx.EndXZ);
                        xzDelta = newXZ - oldXZ;
                    }

                    var hasHeightDelta = math.abs(heightDelta) >= 0.001f;
                    var hasXZDelta     = math.lengthsq(xzDelta) >= 0.000001f;

                    if (!hasHeightDelta && !hasXZDelta) {
                        continue;
                    }

                    // Adjust connected edges that are not part of the path
                    AdjustConnectedEdges(nodeEntity, connectedEdges, pathEdgeSet, heightDelta, xzDelta, hasHeightDelta, hasXZDelta);
                }
            }

            /// <summary>
            /// Gets the cumulative distance at a node index (sum of all edge lengths before this node).
            /// </summary>
            private float GetCumulativeDistanceAtNode(NativeArray<EdgeTransformState> edges, int nodeIndex) {
                if (nodeIndex <= 0) return 0f;
                // The cumulative distance at node i is the cumulative distance of edge i-1 plus its length
                var prevEdge = edges[nodeIndex - 1];
                return prevEdge.CumulativeDistance + prevEdge.Length;
            }

            /// <summary>
            /// Adjusts edges connected to an intersection node that are not part of the path.
            /// </summary>
            private void AdjustConnectedEdges(
                Entity nodeEntity,
                DynamicBuffer<ConnectedEdge> connectedEdges,
                NativeHashSet<Entity> pathEdgeSet,
                float heightDelta,
                float2 xzDelta,
                bool hasHeightDelta,
                bool hasXZDelta) {

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

                    // Adjust endpoint and adjacent control point
                    if (connectedEdge.m_Start == nodeEntity) {
                        if (hasHeightDelta) {
                            bezier.a.y += heightDelta;
                            bezier.b.y += heightDelta;
                        }
                        if (hasXZDelta) {
                            bezier.a.x += xzDelta.x;
                            bezier.a.z += xzDelta.y;
                            bezier.b.x += xzDelta.x;
                            bezier.b.z += xzDelta.y;
                        }
                    } else if (connectedEdge.m_End == nodeEntity) {
                        if (hasHeightDelta) {
                            bezier.d.y += heightDelta;
                            bezier.c.y += heightDelta;
                        }
                        if (hasXZDelta) {
                            bezier.d.x += xzDelta.x;
                            bezier.d.z += xzDelta.y;
                            bezier.c.x += xzDelta.x;
                            bezier.c.z += xzDelta.y;
                        }
                    }

                    var updatedCurve = new Curve { m_Bezier = bezier, m_Length = curve.m_Length };
                    ECB.SetComponent(connectedEdgeEntity, updatedCurve);

                    MarkUpdated(connectedEdgeEntity);
                    MarkUpdated(connectedEdge.m_Start);
                    MarkUpdated(connectedEdge.m_End);
                }
            }

            // ========================================
            // Utility Methods
            // ========================================

            /// <summary>
            /// Marks an entity as updated with Updated and BatchesUpdated components.
            /// </summary>
            private void MarkUpdated(Entity entity) {
                ECB.AddComponent<Updated>(entity);
                ECB.AddComponent<BatchesUpdated>(entity);
            }

            /// <summary>
            /// Marks a node and all its connected edges as updated.
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