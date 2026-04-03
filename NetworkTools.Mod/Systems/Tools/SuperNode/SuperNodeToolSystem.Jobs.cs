namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    public partial class NT_SuperNodeToolSystem {
#if BURST
        [BurstCompile]
#endif
        /// <summary>
        ///     Creates definitions for the merged edge when removing a node.
        /// </summary>
        private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required NativeReference<Entity>           HoveredNode;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Temp>             TempLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required TerrainHeightData                 TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer        RenderBuffer;
            [ReadOnly] public required ToolOutputMode                    OutputMode;
            [ReadOnly] public required NativeList<Entity>                SelectedNodeEntities;
            [ReadOnly] public required NativeHashSet<Entity>             SelectedNodeSet;
            [ReadOnly] public required bool DebugMode;
            public required            NativeHashSet<Entity>             ProcessedEdges;
            public required            EntityCommandBuffer               ECB;

            public void Execute() {
                // Calculate position of the supernode
                // For now, in the future this should be a handle
                var firstNode    = NodeLookup[SelectedNodeEntities[0]];
                var nodePosition = firstNode.m_Position;

                // Process temp entities
                for (var i = 0; i < SelectedNodeEntities.Length; i++) {
                    var nodeEntity     = SelectedNodeEntities[i];
                    var node           = NodeLookup[nodeEntity];
                    var connectedEdges = ConnectedEdgeLookup[nodeEntity];

                    // For every node we need to create a preview edge for its connected edges that shows the new position for the node (while preserving the existing curve)
                    foreach (var connectedEdge in connectedEdges) {
                        var edgeEntity = connectedEdge.m_Edge;

                        // Skip edges we already processed from the other endpoint
                        if (!ProcessedEdges.Add(edgeEntity)) {
                            continue;
                        }

                        var edge        = EdgeLookup[edgeEntity];
                        var curve       = CurveLookup[edgeEntity];
                        var isStartNode = edge.m_Start == nodeEntity;

                        // Mark any edge that connects two of our selected nodes as deleted
                        if (SelectedNodeSet.Contains(isStartNode ? edge.m_End : edge.m_Start)) {
                            ProcessEdgeDeletionDef(edgeEntity, edge);

                            if (DebugMode) {
                                // Debug: Draw the deleted edge in red
                                var curveOfDeletedEdge = CurveLookup[edgeEntity];
                                RenderBuffer.DrawDashedCurve(Color.red, curveOfDeletedEdge.m_Bezier, 1f, 1f, 1f);

                            }

                            continue;
                        }

                        // Recreate the edge with the same nodes and positions except for the moved node
                        var startNodeEntity = edge.m_Start;
                        var startNode       = NodeLookup[edge.m_Start];
                        var startNodePos    = startNode.m_Position;
                        var startNodeRot    = startNode.m_Rotation;
                        var endNodeEntity   = edge.m_End;
                        var endNode         = NodeLookup[edge.m_End];
                        var endNodePos      = endNode.m_Position;
                        var endNodeRot      = endNode.m_Rotation;

                        // Set nodes in such a way that:
                        // - The first node (where the supernode will be) is kept
                        // - Every other node that is being shifted will be re-created (Entity.Null)
                        // - Nodes being shifted get the position of the supernode, while nodes that aren't being shifted keep their position
                        if (isStartNode) {
                            startNodeEntity = Entity.Null;
                            startNodePos    = nodePosition;
                        } else {
                            endNodeEntity = Entity.Null;
                            endNodePos    = nodePosition;
                        }

                        OutputPreviewEdge(edgeEntity,
                                          startNodeEntity,
                                          endNodeEntity,
                                          startNodePos,
                                          endNodePos,
                                          startNodeRot,
                                          endNodeRot,
                                          PrefabRefLookup[edgeEntity].m_Prefab,
                                          curve.m_Bezier,
                                          curve.m_Length);
                    }

                    // When applying, mark all nodes that will be shifted for post-processing
                    if (OutputMode == ToolOutputMode.Apply) {
                        ECB.AddComponent(nodeEntity, new NT_PostProcess { Operation = NT_PostProcessOperation.DeleteNode });
                    }
                }
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

            private void OutputPreviewEdge(Entity     roadEntity,        Entity startNodeEntity, Entity endNodeEntity,
                                           float3     startNodePosition, float3 endNodePosition,
                                           quaternion startNodeRotation, quaternion endNodeRotation,
                                           Entity     prefabEntity,      Bezier4x3 existingBezier, float existingLength
            ) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = roadEntity,
                    m_Prefab   = prefabEntity,
                    m_Flags    = CreationFlags.Recreate | CreationFlags.Parent
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags  = CoursePosFlags.IsRight;
                var endNodeFlags    = CoursePosFlags.IsRight;
                var startElevation  = float2.zero;
                var endElevation    = float2.zero;
                var courseElevation = float2.zero;

                var netCourse = new NetCourse {
                    m_Curve      = existingBezier,
                    m_Length     = existingLength,
                    m_FixedIndex = -1,
                    m_Elevation  = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = startNodeEntity,
                        m_Position      = startNodePosition,
                        m_Rotation      = startNodeRotation,
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = endNodeEntity,
                        m_Position      = endNodePosition,
                        m_Rotation      = endNodeRotation,
                        m_CourseDelta   = 1,
                        m_Elevation     = endElevation,
                        m_Flags         = endNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }
    }
}