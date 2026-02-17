// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_RemoveNodeToolSystem {

#if BURST
        [BurstCompile]
#endif
        private struct MinimalNodeRemovalJob : IJob {
            [ReadOnly] public required Entity m_NodeToRemove;
            [ReadOnly] public required ComponentLookup<Node> m_NodeData;
            [ReadOnly] public required ComponentLookup<Edge> m_EdgeData;
            [ReadOnly] public required ComponentLookup<Curve> m_CurveData;
            [ReadOnly] public required ComponentLookup<Temp> m_TempData;
            [ReadOnly] public required BufferLookup<ConnectedEdge> m_ConnectedEdges;

            public void Execute() {
                // 1. Get the two connected edges
                var connectedEdges = m_ConnectedEdges[m_NodeToRemove];
                var edge1 = connectedEdges[0].m_Edge;
                var edge2 = connectedEdges[1].m_Edge;

                // 2. Determine edge orientation relative to node
                var edgeData1 = m_EdgeData[edge1];
                var edgeData2 = m_EdgeData[edge2];
                var edge1StartsAtNode = edgeData1.m_Start == m_NodeToRemove;
                var edge2EndsAtNode = edgeData2.m_End == m_NodeToRemove;

                // 3. Get curves and orient them: edge1 → node → edge2
                var curve1 = m_CurveData[edge1];
                var curve2 = m_CurveData[edge2];
                if (edge1StartsAtNode) {
                    curve1.m_Bezier = MathUtils.Invert(curve1.m_Bezier);
                }

                if (edge2EndsAtNode) {
                    curve2.m_Bezier = MathUtils.Invert(curve2.m_Bezier);
                }

                // 4. Join curves into one
                var joinedCurve = MathUtils.Join(curve1.m_Bezier, curve2.m_Bezier);

                // 5. Mark node for deletion
                var nodeTemp = m_TempData[m_NodeToRemove];
                nodeTemp.m_Flags           = TempFlags.Delete | TempFlags.Hidden;
                m_TempData[m_NodeToRemove] = nodeTemp;

                // 6. Update surviving edge (edge2) with joined curve
                curve2.m_Bezier = joinedCurve;
                if (edge2EndsAtNode) {
                    curve2.m_Bezier = MathUtils.Invert(curve2.m_Bezier);
                }

                curve2.m_Length    = MathUtils.Length(curve2.m_Bezier);
                m_CurveData[edge2] = curve2;

                // 7. Update surviving edge's connection to far node of deleted edge
                var farNode = edge1StartsAtNode ? edgeData1.m_End : edgeData1.m_Start;
                if (edge2EndsAtNode) {
                    edgeData2.m_End = farNode;
                }
                else {
                    edgeData2.m_Start = farNode;
                }

                m_EdgeData[edge2] = edgeData2;

                // 8. Update far node's ConnectedEdge buffer (switch edge1 → edge2)
                var farNodeEdges = m_ConnectedEdges[farNode];
                for (var i = 0; i < farNodeEdges.Length; i++)
                    if (farNodeEdges[i].m_Edge == edge1) {
                        farNodeEdges[i] = new ConnectedEdge(edge2);
                        break;
                    }

                // 9. Mark deleted edge
                var edge1Temp = m_TempData[edge1];
                edge1Temp.m_Flags = TempFlags.Delete | TempFlags.Hidden;
                m_TempData[edge1] = edge1Temp;
            }
        }


#if BURST
        [BurstCompile]
#endif
        /// <summary>
        ///     Creates definitions for the merged edge when removing a node.
        ///     Given a node with exactly 2 connected edges, creates a NetCourse that
        ///     connects the two neighbor nodes, effectively previewing the removal.
        /// </summary>
        private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required NativeReference<Entity> HoveredNode;
            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required TerrainHeightData TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer RenderBuffer;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                var nodeToRemove = HoveredNode.Value;

                // Validate we have a node to work with
                if (nodeToRemove == Entity.Null) {
                    return;
                }

                // Get the connected edges buffer
                if (!ConnectedEdgeLookup.TryGetBuffer(nodeToRemove, out var connectedEdges)) {
                    return;
                }

                // Should have exactly 2 edges (already filtered by eligibility)
                if (connectedEdges.Length != 2) {
                    return;
                }

                var edge1Entity = connectedEdges[0].m_Edge;
                var edge2Entity = connectedEdges[1].m_Edge;

                // Get edge data
                if (!EdgeLookup.TryGetComponent(edge1Entity, out var edge1) ||
                    !EdgeLookup.TryGetComponent(edge2Entity, out var edge2)) {
                    return;
                }


                // Get curve data for both edges
                if (!CurveLookup.TryGetComponent(edge1Entity, out var curve1) ||
                    !CurveLookup.TryGetComponent(edge2Entity, out var curve2)) {
                    return;
                }

                // Create the new merged curve (as a new edge)
                ProcessNewCurveDef(edge1, nodeToRemove, edge2, curve1, curve2, edge1Entity);
                //ProcessEdgeDeletionDef(edge1Entity, edge1);
                ProcessEdgeDeletionDef(edge2Entity, edge2);
                //ProcessNodeDeletionDef(nodeToRemove);
            }

            /// <summary>
            ///     Processes the edge definition for deletion.
            /// </summary>
            private void ProcessEdgeDeletionDef(Entity edgeEntity, Edge edge) {
                var definitionEntity = ECB.CreateEntity();
                var creationDefinition = new CreationDefinition {
                    m_Original = edgeEntity,
                    m_Flags    = CreationFlags.Delete | CreationFlags.Hidden
                };

                var curve = CurveLookup[edgeEntity];
                var netCourse = new NetCourse {
                    m_Curve      = curve.m_Bezier,
                    m_Length     = MathUtils.Length(curve.m_Bezier),
                    m_FixedIndex = -1,
                    m_StartPosition = new CoursePos {
                        m_Entity      = edge.m_Start,
                        m_Position    = curve.m_Bezier.a,
                        m_Rotation    = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve.m_Bezier)),
                        m_CourseDelta = 0f
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity      = edge.m_End,
                        m_Position    = curve.m_Bezier.d,
                        m_Rotation    = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve.m_Bezier)),
                        m_CourseDelta = 1f
                    }
                };


                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent(definitionEntity, netCourse);
                ECB.AddComponent<Updated>(definitionEntity);
            }

            /// <summary>
            ///     Processes the node definition for removal.
            /// </summary>
            /// <param name="nodeToRemove"></param>
            private void ProcessNodeDeletionDef(Entity nodeToRemove) {
                var definitionEntity = ECB.CreateEntity();
                var creationDefinition = new CreationDefinition {
                    m_Original = nodeToRemove,
                    m_Flags    = CreationFlags.Delete | CreationFlags.Hidden
                };
                var node = NodeLookup[nodeToRemove];
                var netCourse = new NetCourse {
                    m_Curve =
                        new Bezier4x3(node.m_Position, node.m_Position, node.m_Position, node.m_Position),
                    m_Length     = 0f,
                    m_FixedIndex = -1,
                    m_StartPosition = new CoursePos {
                        m_Entity      = nodeToRemove,
                        m_Position    = node.m_Position,
                        m_Rotation    = node.m_Rotation,
                        m_CourseDelta = 0f,
                        m_Flags = CoursePosFlags.IsLeft |
                                  CoursePosFlags.IsRight
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity      = nodeToRemove,
                        m_Position    = node.m_Position,
                        m_Rotation    = node.m_Rotation,
                        m_CourseDelta = 1f,
                        m_Flags = CoursePosFlags.IsLeft |
                                  CoursePosFlags.IsRight
                    }
                };
                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent(definitionEntity, netCourse);
                ECB.AddComponent<Updated>(definitionEntity);
            }

            /// <summary>
            ///     Processes the curve definition for the merged edge.
            /// </summary>
            /// <param name="edge1"></param>
            /// <param name="nodeToRemove"></param>
            /// <param name="edge2"></param>
            /// <param name="curve1"></param>
            /// <param name="curve2"></param>
            /// <param name="edge1Entity"></param>
            private void ProcessNewCurveDef(Edge edge1,
                Entity nodeToRemove,
                Edge edge2,
                Curve curve1,
                Curve curve2,
                Entity edge1Entity) {
                // Determine the neighbor nodes (the nodes that are NOT the one being removed)
                var neighbor1 = edge1.m_Start == nodeToRemove ? edge1.m_End : edge1.m_Start;
                var neighbor2 = edge2.m_Start == nodeToRemove ? edge2.m_End : edge2.m_Start;

                // Compute the new bezier curve that connects neighbor1 to neighbor2
                // by retaining the original control points from each edge
                var newBezier = ComputeMergedBezier(nodeToRemove,
                    edge1,
                    curve1,
                    edge2,
                    curve2);

                // Create the definition entity
                var definitionEntity = ECB.CreateEntity();

                // CreationDefinition - use first edge's prefab
                var creationDefinition = new CreationDefinition {
                    m_Original = edge1Entity,
                    m_Flags    = CreationFlags.Recreate
                };

                if (PrefabRefLookup.TryGetComponent(edge1Entity, out var prefabRef)) {
                    creationDefinition.m_Prefab = new PrefabRef(prefabRef.m_Prefab);
                }

                if (PseudoRandomSeedLookup.TryGetComponent(edge1Entity, out var seed)) {
                    creationDefinition.m_RandomSeed = seed.m_Seed;
                }

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                // Create NetCourse component
                var netCourse = new NetCourse {
                    m_Curve      = newBezier,
                    m_Length     = MathUtils.Length(newBezier),
                    m_FixedIndex = -1,
                    m_Elevation  = default,
                    m_StartPosition = new CoursePos {
                        m_Entity        = neighbor1,
                        m_Position      = newBezier.a,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(newBezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = default,
                        m_Flags         = CoursePosFlags.IsFirst,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = neighbor2,
                        m_Position      = newBezier.d,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(newBezier)),
                        m_CourseDelta   = 1,
                        m_Elevation     = default,
                        m_Flags         = CoursePosFlags.IsLast,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }

            /// <summary>
            ///     Computes a merged bezier curve connecting two neighbor nodes,
            ///     by retaining the original control points from each edge.
            ///     This preserves the exact original curve shape at each end.
            /// </summary>
            private Bezier4x3 ComputeMergedBezier(Entity nodeToRemove,
                Edge edge1,
                Curve curve1,
                Edge edge2,
                Curve curve2) {
                // New bezier goes: neighbor1 -> neighbor2
                // We take a, b from edge1 (neighbor1 side) and c, d from edge2 (neighbor2 side)

                float3 a, b, c, d;

                // Edge1: get a, b from the neighbor1 side
                if (edge1.m_Start != nodeToRemove) {
                    // edge1: neighbor1 -> nodeToRemove
                    // neighbor1 is at the start (a side)
                    a = curve1.m_Bezier.a;
                    b = curve1.m_Bezier.b;
                }
                else {
                    // edge1: nodeToRemove -> neighbor1
                    // neighbor1 is at the end (d side)
                    a = curve1.m_Bezier.d;
                    b = curve1.m_Bezier.c;
                }

                // Edge2: get c, d from the neighbor2 side
                if (edge2.m_End != nodeToRemove) {
                    // edge2: nodeToRemove -> neighbor2
                    // neighbor2 is at the end (d side)
                    c = curve2.m_Bezier.c;
                    d = curve2.m_Bezier.d;
                }
                else {
                    // edge2: neighbor2 -> nodeToRemove
                    // neighbor2 is at the start (a side)
                    c = curve2.m_Bezier.b;
                    d = curve2.m_Bezier.a;
                }

                return new Bezier4x3 { a = a, b = b, c = c, d = d };
            }
        }
    }
}