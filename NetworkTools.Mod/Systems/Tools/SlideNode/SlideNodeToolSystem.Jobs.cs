// <copyright file="SlideNodeToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_SlideNodeToolSystem {
#if USE_BURST
        [BurstCompile]
#endif
        /// <summary>
        /// Snaps the curve parameter on the parent bezier so the node doesn't slide
        /// too close to either neighbor node.
        /// </summary>
        private struct SnapControlPointJob : IJob {
            [ReadOnly] public required Entity Edge1Entity;
            [ReadOnly] public required Entity Edge2Entity;
            [ReadOnly] public required Entity NodeEntity;
            [ReadOnly] public required float RawCurvePosition;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData> NetGeometryDataLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            public required NativeReference<float> SnappedCurvePosition;
            public required NativeReference<float3> SnappedHitPosition;
            public required NativeReference<Bezier4x3> ParentBezier;

            public void Execute() {
                if (!EdgeLookup.TryGetComponent(Edge1Entity, out var edge1) ||
                    !EdgeLookup.TryGetComponent(Edge2Entity, out var edge2) ||
                    !CurveLookup.TryGetComponent(Edge1Entity, out var curve1) ||
                    !CurveLookup.TryGetComponent(Edge2Entity, out var curve2)) {
                    SnappedCurvePosition.Value = RawCurvePosition;
                    return;
                }

                // Recover the parent bezier from the two child edges
                var parentBezier = NT_EdgeUtils.ComputeSimpleMergedBezier(NodeEntity, edge1, curve1, edge2, curve2);
                ParentBezier.Value = parentBezier;

                // Calculate endpoint constraints based on neighbor connectivity
                var neighbor1 = edge1.m_Start == NodeEntity ? edge1.m_End : edge1.m_Start;
                var neighbor2 = edge2.m_Start == NodeEntity ? edge2.m_End : edge2.m_Start;

                var parentLength = MathUtils.Length(parentBezier);

                var minCurvePosition = 0f;
                var maxCurvePosition = 1f;

                // Determine connectivity and geometry for start neighbor
                var startConnected = ConnectedEdgeLookup.TryGetBuffer(neighbor1, out var startEdges) && startEdges.Length > 1;
                // Determine connectivity and geometry for end neighbor
                var endConnected = ConnectedEdgeLookup.TryGetBuffer(neighbor2, out var endEdges) && endEdges.Length > 1;

                // Use the edge1 prefab for geometry data (both edges should be the same type)
                if (PrefabRefLookup.TryGetComponent(Edge1Entity, out var prefabRef) &&
                    NetGeometryDataLookup.TryGetComponent(prefabRef.m_Prefab, out var netGeometry)) {
                    NT_EdgeUtils.GetMinMaxSplitPositions(
                        parentLength,
                        netGeometry.m_DefaultWidth,
                        netGeometry.m_EdgeLengthRange.min,
                        startConnected,
                        endConnected,
                        out minCurvePosition,
                        out maxCurvePosition);
                }

                // Clamp the raw position within valid range
                var clampedPosition = math.clamp(RawCurvePosition, minCurvePosition, maxCurvePosition);

                SnappedCurvePosition.Value = clampedPosition;
                SnappedHitPosition.Value = MathUtils.Position(parentBezier, clampedPosition);
            }
        }

#if USE_BURST
        [BurstCompile]
#endif
        /// <summary>
        /// Creates definition entities for the slid node: two new edge curves (from Divide)
        /// and optionally deletes/recreates the original edges.
        /// </summary>
        private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required Entity NodeEntity;
            [ReadOnly] public required Entity Edge1Entity;
            [ReadOnly] public required Entity Edge2Entity;
            [ReadOnly] public required float3 HitPosition;
            [ReadOnly] public required float CurvePosition;
            [ReadOnly] public required Bezier4x3 ParentBezier;
            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required TerrainHeightData TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer RenderBuffer;
            [ReadOnly] public required ToolOutputMode OutputMode;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                if (NodeEntity == Entity.Null || Edge1Entity == Entity.Null || Edge2Entity == Entity.Null) {
                    return;
                }

                if (!EdgeLookup.TryGetComponent(Edge1Entity, out var edge1) ||
                    !EdgeLookup.TryGetComponent(Edge2Entity, out var edge2) ||
                    !CurveLookup.TryGetComponent(Edge1Entity, out var curve1) ||
                    !CurveLookup.TryGetComponent(Edge2Entity, out var curve2)) {
                    return;
                }

                // Determine the neighbor nodes (nodes that are NOT the sliding node)
                var neighbor1 = edge1.m_Start == NodeEntity ? edge1.m_End : edge1.m_Start;
                var neighbor2 = edge2.m_Start == NodeEntity ? edge2.m_End : edge2.m_Start;

                // Subdivide parent bezier at the new parameter
                MathUtils.Divide(ParentBezier, out var newCurve1, out var newCurve2, CurvePosition);
                // newCurve1: neighbor1 → new node position
                // newCurve2: new node position → neighbor2

                if (OutputMode == ToolOutputMode.Preview) {
                    OutputPreview(edge1, edge2, neighbor1, neighbor2, newCurve1, newCurve2);
                } else {
                    OutputApply(edge1, edge2, neighbor1, neighbor2, newCurve1, newCurve2);
                }
            }

            private void OutputPreview(Edge edge1, Edge edge2, Entity neighbor1, Entity neighbor2,
                                        Bezier4x3 newCurve1, Bezier4x3 newCurve2) {
                // Recreate edge1: neighbor1 → node (with new curve shape)
                ProcessEdgeDef(Edge1Entity, neighbor1, NodeEntity, newCurve1);
                // Recreate edge2: node → neighbor2 (with new curve shape)
                ProcessEdgeDef(Edge2Entity, NodeEntity, neighbor2, newCurve2);
            }

            private void OutputApply(Edge edge1, Edge edge2, Entity neighbor1, Entity neighbor2,
                                      Bezier4x3 newCurve1, Bezier4x3 newCurve2) {
                // Recreate edge1: neighbor1 → node (with new curve shape)
                ProcessEdgeDef(Edge1Entity, neighbor1, NodeEntity, newCurve1);
                // Recreate edge2: node → neighbor2 (with new curve shape)
                ProcessEdgeDef(Edge2Entity, NodeEntity, neighbor2, newCurve2);
            }

            /// <summary>
            /// Creates a definition entity for a single edge with the given bezier curve.
            /// </summary>
            private void ProcessEdgeDef(Entity originalEdge, Entity startNode, Entity endNode, Bezier4x3 bezier) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = originalEdge,
                    m_Flags = CreationFlags.Parent | CreationFlags.Recreate
                };

                if (PrefabRefLookup.TryGetComponent(originalEdge, out var prefabRef)) {
                    creationDefinition.m_Prefab = new PrefabRef(prefabRef.m_Prefab);
                }

                if (PseudoRandomSeedLookup.TryGetComponent(originalEdge, out var seed)) {
                    creationDefinition.m_RandomSeed = seed.m_Seed;
                }

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var netCourse = new NetCourse {
                    m_Curve = bezier,
                    m_Length = MathUtils.Length(bezier),
                    m_FixedIndex = -1,
                    m_Elevation = default,
                    m_StartPosition = new CoursePos {
                        m_Entity = startNode,
                        m_Position = bezier.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                        m_CourseDelta = 0,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsFirst,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = endNode,
                        m_Position = bezier.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier)),
                        m_CourseDelta = 1,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsLast,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }
    }
}
