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
            public required            EntityCommandBuffer               ECB;

            public void Execute() {
                // Calculate position of the supernode
                // For now, in the future this should be a handle
                var firstNode    = NodeLookup[SelectedNodeEntities[0]];
                var nodePosition = firstNode.m_Position;

                // Process temp entities
                foreach (var nodeEntity in SelectedNodeEntities) {
                    var node           = NodeLookup[nodeEntity];
                    var connectedEdges = ConnectedEdgeLookup[nodeEntity];

                    // For every node we need to create a preview edge for its connected edges that shows the new position for the node (while preserving the existing curve)
                    foreach (var connectedEdge in connectedEdges) {
                        var edgeEntity = connectedEdge.m_Edge;
                        var edge       = EdgeLookup[edgeEntity];
                        var curve      = CurveLookup[edgeEntity];

                        // Recreate the edge with the same nodes and positions except for the moved node
                        var startNodeEntity = edge.m_Start;
                        var startNode       = NodeLookup[edge.m_Start];
                        var startNodePos    = edge.m_Start == nodeEntity ? nodePosition : startNode.m_Position;
                        var endNodeEntity   = edge.m_End;
                        var endNode         = NodeLookup[edge.m_End];
                        var endNodePos      = edge.m_End == nodeEntity ? nodePosition : endNode.m_Position;


                        OutputPreviewEdge(edgeEntity,
                                          startNodeEntity,
                                          endNodeEntity,
                                          startNodePos,
                                          endNodePos,
                                          PrefabRefLookup[edgeEntity].m_Prefab,
                                          curve.m_Bezier,
                                          curve.m_Length);

                        // Mark any edge that connects two of our selected nodes as deleted
                        // todo
                    }
                }
            }

            private float3 CalculateNodePosition() {
                var totalPosition = float3.zero;

                foreach (var nodeEntity in SelectedNodeEntities) {
                    var node = NodeLookup[nodeEntity];
                    totalPosition += node.m_Position;
                }

                return totalPosition / SelectedNodeEntities.Length;
            }

            private void OutputPreviewEdge(Entity roadEntity,        Entity    startNodeEntity, Entity endNodeEntity,
                                           float3 startNodePosition, float3    endNodePosition,
                                           Entity prefabEntity,      Bezier4x3 existingBezier, float existingLength
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
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(existingBezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = endNodeEntity,
                        m_Position      = endNodePosition,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(existingBezier)),
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