// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Net;
    using Game.Common;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Rendering;
    using Game.Tools;
    using Colossal.Mathematics;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_AddNodeToolSystem {
#if BURST
        [BurstCompile]
#endif
        private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required Entity EdgeEntity;
            [ReadOnly] public required float3 HitPosition;
            [ReadOnly] public required float CurvePosition;
            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required TerrainHeightData TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer RenderBuffer;
            public required ToolOutputMode OutputMode;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                // Validate we have an edge to work with
                if (EdgeEntity == Entity.Null) {
                    return;
                }

                // Get edge and curve data
                if (!EdgeLookup.TryGetComponent(EdgeEntity, out var edge)) {
                    return;
                }
                if (!CurveLookup.TryGetComponent(EdgeEntity, out var curve)) {
                    return;
                }
                if (!PrefabRefLookup.TryGetComponent(EdgeEntity, out var prefabRef)) {
                    return;
                }
                if (!PseudoRandomSeedLookup.TryGetComponent(EdgeEntity, out var seed)) {
                    return;
                }

                // For now, no difference between preview and apply - both create the same definition entities
                if (OutputMode == ToolOutputMode.Preview)
                {
                    OutputPreview(edge, curve, prefabRef, seed);
                } else {
                    OutputPreview(edge, curve, prefabRef, seed);
                }
            }

            private void OutputPreview(Edge edge, Curve curve, PrefabRef prefabRef, PseudoRandomSeed seed) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Flags    = CreationFlags.Recreate
                };

                if (prefabRef.m_Prefab != Entity.Null)
                {
                    creationDefinition.m_Prefab = prefabRef;
                }
                creationDefinition.m_RandomSeed = seed.m_Seed;

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var netCourse = new NetCourse {
                    m_Curve = new Bezier4x3(HitPosition, HitPosition, HitPosition, HitPosition),
                    m_Length = 0,
                    m_FixedIndex = -1,
                    m_Elevation = default,
                    m_StartPosition = new CoursePos {
                        m_Entity = EdgeEntity,
                        m_Position = HitPosition,
                        m_Rotation = default,
                        m_CourseDelta = 0,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                        m_ParentMesh = -1,
                        m_SplitPosition = CurvePosition
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = EdgeEntity,
                        m_Position = HitPosition,
                        m_Rotation = default,
                        m_CourseDelta = 1,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                        m_ParentMesh = -1,
                        m_SplitPosition = CurvePosition
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }


            private void OutputApply(Edge edge, Curve curve, PrefabRef prefabRef, PseudoRandomSeed seed) {

            }

            /// <summary>
            /// Processes the curve definition to add a new node to an edge.
            /// Splits the edge's bezier curve at the hit position, creating two new edges
            /// connected by a new node.
            /// </summary>
            private void ProcessAddNodeDef(Edge edge, Curve curve, Entity edgeEntity) {
                // Use MathUtils.Divide to split the bezier at the curve position
                // This preserves the exact curve shape
                MathUtils.Divide(curve.m_Bezier, out var bezier1, out var bezier2, CurvePosition);

                // The new node position is at the split point (bezier1.d == bezier2.a)
                var newNodePosition = bezier1.d;

                // Get prefab info from original edge
                PrefabRefLookup.TryGetComponent(edgeEntity, out var prefabRef);
                PseudoRandomSeedLookup.TryGetComponent(edgeEntity, out var seed);

                // === Create first edge definition (original start -> new node) ===
                var definition1Entity = ECB.CreateEntity();

                var creationDefinition1 = new CreationDefinition {
                    m_Original = edgeEntity,
                    m_Flags = CreationFlags.Recreate
                };

                if (prefabRef.m_Prefab != Entity.Null) {
                    creationDefinition1.m_Prefab = prefabRef;
                }
                creationDefinition1.m_RandomSeed = seed.m_Seed;

                ECB.AddComponent(definition1Entity, creationDefinition1);
                ECB.AddComponent<Updated>(definition1Entity);

                var netCourse1 = new NetCourse {
                    m_Curve = bezier1,
                    m_Length = MathUtils.Length(bezier1),
                    m_FixedIndex = -1,
                    m_Elevation = default,
                    m_StartPosition = new CoursePos {
                        m_Entity = edge.m_Start,
                        m_Position = bezier1.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier1)),
                        m_CourseDelta = 0,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsFirst,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = Entity.Null, // New node - will be created by the system
                        m_Position = bezier1.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier1)),
                        m_CourseDelta = 1,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsLast,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definition1Entity, netCourse1);

                // === Create second edge definition (new node -> original end) ===
                var definition2Entity = ECB.CreateEntity();

                var creationDefinition2 = new CreationDefinition {
                    m_Original = Entity.Null, // New edge, not replacing anything
                    m_Flags = default
                };

                if (prefabRef.m_Prefab != Entity.Null) {
                    creationDefinition2.m_Prefab = prefabRef;
                }
                creationDefinition2.m_RandomSeed = seed.m_Seed;

                ECB.AddComponent(definition2Entity, creationDefinition2);
                ECB.AddComponent<Updated>(definition2Entity);

                var netCourse2 = new NetCourse {
                    m_Curve = bezier2,
                    m_Length = MathUtils.Length(bezier2),
                    m_FixedIndex = -1,
                    m_Elevation = default,
                    m_StartPosition = new CoursePos {
                        m_Entity = Entity.Null, // Same new node as end of first edge
                        m_Position = bezier2.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier2)),
                        m_CourseDelta = 0,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsFirst,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = edge.m_End,
                        m_Position = bezier2.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier2)),
                        m_CourseDelta = 1,
                        m_Elevation = default,
                        m_Flags = CoursePosFlags.IsLast,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definition2Entity, netCourse2);
            }
        }
    }
}