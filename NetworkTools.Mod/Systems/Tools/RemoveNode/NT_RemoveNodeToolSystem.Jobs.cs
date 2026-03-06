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
            public required            EntityCommandBuffer               ECB;

            public void Execute() {
                var nodeEntity = HoveredNode.Value;

                // Validate we have a node to work with
                if (nodeEntity == Entity.Null) {
                    return;
                }

                // Get the connected edges buffer
                if (!ConnectedEdgeLookup.TryGetBuffer(nodeEntity, out var connectedEdges)) {
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


                // For now, no difference between preview and apply - both create the same definition entities
                if (OutputMode == ToolOutputMode.Preview) {
                    OutputPreview(edge1Entity, edge2Entity, nodeEntity, edge1, edge2, curve1, curve2);
                } else {
                    OutputApply(edge1Entity, edge2Entity, nodeEntity, edge1, edge2, curve1, curve2);
                }
            }

            private void OutputPreview(Entity edge1Entity, Entity edge2Entity, Entity nodeEntity, Edge edge1,
                                       Edge   edge2,       Curve  curve1,      Curve  curve2) {
                // Create the new merged curve (as a new edge)
                ProcessNewCurveDef(nodeEntity, edge1, edge2, curve1, curve2, edge1Entity);
                // Delete the redundant edge
                ProcessEdgeDeletionDef(edge2Entity, edge2);
            }

            private void OutputApply(Entity edge1Entity, Entity edge2Entity, Entity nodeEntity, Edge edge1,
                           Edge edge2, Curve curve1, Curve curve2) {
                // Create the new merged curve (as a new edge)
                ProcessNewCurveDef(nodeEntity, edge1, edge2, curve1, curve2, edge1Entity);
                // Delete the redundant edge
                ProcessEdgeDeletionDef(edge2Entity, edge2);
                // Delete the node
                ECB.AddComponent(nodeEntity, new Deleted());
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
            ///     Processes the curve definition for the merged edge.
            /// </summary>
            /// <param name="edge1"></param>
            /// <param name="nodeEntity"></param>
            /// <param name="edge2"></param>
            /// <param name="curve1"></param>
            /// <param name="curve2"></param>
            /// <param name="edge1Entity"></param>
            private void ProcessNewCurveDef(
                Entity nodeEntity,
                Edge   edge1,
                Edge   edge2,
                Curve  curve1,
                Curve  curve2,
                Entity edge1Entity) {
                // Determine the neighbor nodes (the nodes that are NOT the one being removed)
                var neighbor1 = edge1.m_Start == nodeEntity ? edge1.m_End : edge1.m_Start;
                var neighbor2 = edge2.m_Start == nodeEntity ? edge2.m_End : edge2.m_Start;

                // Compute the new bezier curve that connects neighbor1 to neighbor2
                // by retaining the original control points from each edge
                var newBezier = ComputeMergedBezier(nodeEntity,
                                                    edge1,
                                                    curve1,
                                                    edge2,
                                                    curve2);

                // Create the definition entity
                var definitionEntity = ECB.CreateEntity();

                // CreationDefinition - use first edge's prefab
                var creationDefinition = new CreationDefinition {
                    m_Original = edge1Entity,
                    m_Flags    = CreationFlags.Parent | CreationFlags.Recreate
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
            ///     Computes a merged bezier curve connecting two neighbor nodes.
            /// </summary>
            private Bezier4x3 ComputeMergedBezier(Entity nodeEntity,
                                                  Edge   edge1,
                                                  Curve  curve1,
                                                  Edge   edge2,
                                                  Curve  curve2) {
                // Orient each curve so .a is at the neighbor end (pointing away from the removed node).
                var b1 = edge1.m_Start == nodeEntity ? MathUtils.Invert(curve1.m_Bezier) : curve1.m_Bezier;
                var b2 = edge2.m_End   == nodeEntity ? MathUtils.Invert(curve2.m_Bezier) : curve2.m_Bezier;

                return new Bezier4x3 { a = b1.a, b = b1.b, c = b2.c, d = b2.d };
            }
        }
    }
}