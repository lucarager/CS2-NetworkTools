// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems {
    using Game.Net;
    using Game.Common;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Rendering;
    using Game.Tools;
    using Colossal.Mathematics;
    #region Using Statements

    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_AddNodeToolSystem {
        public enum SnapMode : uint {
            None     = 0,
            RoadSide = 1
        }

        private static class SnapLevel {
            public const float RoadSide = 2f;
        }

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

                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Flags    = CreationFlags.Recreate
                };

                if (prefabRef.m_Prefab != Entity.Null) {
                    creationDefinition.m_Prefab = prefabRef;
                }
                creationDefinition.m_RandomSeed = seed.m_Seed;

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var netCourse = new NetCourse {
                    m_Curve      = new Bezier4x3(HitPosition, HitPosition, HitPosition, HitPosition),
                    m_Length     = 0,
                    m_FixedIndex = -1,
                    m_Elevation  = default,
                    m_StartPosition = new CoursePos {
                        m_Entity        = EdgeEntity,
                        m_Position      = HitPosition,
                        m_Rotation      = default,
                        m_CourseDelta   = 0,
                        m_Elevation     = default,
                        m_Flags         = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                        m_ParentMesh    = -1,
                        m_SplitPosition = CurvePosition
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = EdgeEntity, 
                        m_Position      = HitPosition,
                        m_Rotation      = default,
                        m_CourseDelta   = 1,
                        m_Elevation     = default,
                        m_Flags = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                        m_ParentMesh    = -1,
                        m_SplitPosition = CurvePosition
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);

                // Process the split - add a new node to the edge
                //ProcessAddNodeDef(edge, curve, EdgeEntity);
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

#if BURST
        [BurstCompile]
#endif
        private struct SnapJob : IJob {
            [ReadOnly] public required NativeList<Game.Tools.ControlPoint> m_ControlPoints;
            [ReadOnly] public required SnapMode                            m_SnapMode;

            [ReadOnly]
            public required Colossal.Collections.NativeQuadTree<Entity, Game.Common.QuadTreeBoundsXZ>
                m_NetTree;

            [ReadOnly] public required Game.Simulation.TerrainHeightData m_TerrainHeightData;

            [ReadOnly]
            public required Game.Simulation.WaterSurfaceData<Game.Simulation.SurfaceWater>
                m_WaterSurfaceData;

            [ReadOnly] public required EntityTypeHandle                        m_EntityTypeHandle;
            [ReadOnly] public required ComponentLookup<Game.Net.Node>          m_NodeLookup;
            [ReadOnly] public required ComponentLookup<Game.Net.Edge>          m_EdgeLookup;
            [ReadOnly] public required ComponentLookup<Game.Net.Curve>         m_CurveLookup;
            [ReadOnly] public required ComponentLookup<Game.Net.Composition>   m_CompositionLookup;
            [ReadOnly] public required ComponentLookup<Game.Prefabs.PrefabRef> m_PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<Game.Prefabs.NetData>   m_NetDataLookup;

            [ReadOnly]
            public required ComponentLookup<Game.Prefabs.NetGeometryData> m_NetGeometryDataLookup;

            [ReadOnly]
            public required ComponentLookup<Game.Prefabs.NetCompositionData> m_NetCompositionDataLookup;

            [ReadOnly] public required ComponentLookup<Game.Net.EdgeGeometry> m_EdgeGeoLookup;
            [ReadOnly] public required ComponentLookup<Game.Net.StartNodeGeometry> m_StartNodeGeoLookup;
            [ReadOnly] public required ComponentLookup<Game.Net.EndNodeGeometry> m_EndNodeGeoLookup;
            [ReadOnly] public required BufferLookup<Game.Net.ConnectedEdge> m_ConnectedEdgeLookup;
            [ReadOnly] public required BufferLookup<Game.Zones.SubBlock> m_SubBlockLookup;
            public required            NativeReference<bool> m_IsSnapped;

            public void Execute() {
                // Process last control point
                var controlPoint = m_ControlPoints[^1];

                // Search radius around current point
                var searchRadius = 20f;
                var bounds = new Colossal.Mathematics.Bounds3(
                                                              controlPoint.m_Position - searchRadius,
                                                              controlPoint.m_Position + searchRadius
                                                             );

                // Start with the raw control point (no snap)
                var bestSnapPosition = controlPoint;
                bestSnapPosition.m_SnapPriority = float2.zero; // No snap = lowest priority

                // Snap Start
                // ========================

                // Run ALL enabled snap modes - each one only updates bestSnapPosition if it wins
                if ((m_SnapMode & SnapMode.RoadSide) != 0)
                    HandleRoadSideSnap(ref bestSnapPosition, controlPoint, bounds, searchRadius);

                // Snap End
                // ========================

                // Check if we snapped
                var isSnapped = !controlPoint.m_Position.Equals(bestSnapPosition.m_Position) ||
                                !controlPoint.m_Rotation.Equals(bestSnapPosition.m_Rotation) ||
                                !controlPoint.m_Direction.Equals(bestSnapPosition.m_Direction);
                m_IsSnapped.Value = isSnapped;

                // Exit early on no snap
                if (!isSnapped) return;

                // Calculate height
                // todo do we need this?
                //CalculateHeight(ref bestSnapPosition, parcelData, float.MinValue);

                // Update control point
                m_ControlPoints[^1] = bestSnapPosition;
            }

            public void HandleRoadSideSnap(ref Game.Tools.ControlPoint  bestSnapPosition,
                                           Game.Tools.ControlPoint      controlPoint,
                                           Colossal.Mathematics.Bounds3 bounds,
                                           float                        minDistance) {
                //var iterator = new EdgeSnapIterator {
                //    m_TotalBounds              = bounds,
                //    m_Bounds                   = bounds,
                //    m_SnapOffset               = 20f,
                //    m_Elevation                = controlPoint.m_Elevation,
                //    m_HeightRange              = new Bounds1(-20f, 20f),
                //    m_NetData                  = default,
                //    m_ControlPoint             = controlPoint,
                //    m_BestSnapPosition         = bestSnapPosition,
                //    m_TerrainHeightData        = m_TerrainHeightData,
                //    m_WaterSurfaceData         = m_WaterSurfaceData,
                //    m_NodeLookup               = m_NodeLookup,
                //    m_EdgeLookup               = m_EdgeLookup,
                //    m_CurveLookup              = m_CurveLookup,
                //    m_SubBlockLookup           = m_SubBlockLookup,
                //    m_CompositionLookup        = m_CompositionLookup,
                //    m_PrefabRefLookup          = m_PrefabRefLookup,
                //    m_NetDataLookup            = m_NetDataLookup,
                //    m_NetGeometryDataLookup    = m_NetGeometryDataLookup,
                //    m_NetCompositionDataLookup = m_NetCompositionDataLookup,
                //    m_EdgeGeoLookup            = m_EdgeGeoLookup,
                //    m_StartNodeGeoLookup       = m_StartNodeGeoLookup,
                //    m_EndNodeGeoLookup         = m_EndNodeGeoLookup,
                //    m_ConnectedEdgeLookup      = m_ConnectedEdgeLookup,
                //    m_BestDistance             = minDistance,
                //    m_SnapLevel                = SnapLevel.RoadSide,
                //};

                //m_NetTree.Iterate(ref iterator);

                //AddSnapPosition(ref bestSnapPosition, iterator.BestSnapPosition);
            }

            private static void AddSnapPosition(ref Game.Tools.ControlPoint bestSnapPosition,
                                                Game.Tools.ControlPoint     candidate) {
                if (CompareSnapPriority(candidate.m_SnapPriority, bestSnapPosition.m_SnapPriority))
                    bestSnapPosition = candidate;
            }

            private static bool CompareSnapPriority(float2 a, float2 b) {
                // Higher level always wins; if equal level, compare priority
                return a.x > b.x || (a.x == b.x && a.y > b.y);
            }

            public struct EdgeSnapIterator : Colossal.Collections.INativeQuadTreeIterator<Entity,
                Game.Common.QuadTreeBoundsXZ> {
                public          Game.Tools.ControlPoint BestSnapPosition => m_BestSnapPosition;
                public required Colossal.Mathematics.Bounds3 m_TotalBounds;
                public required Colossal.Mathematics.Bounds3 m_Bounds;
                public required float m_SnapOffset;
                public required float m_Elevation;
                public required Colossal.Mathematics.Bounds1 m_HeightRange;
                public required Game.Prefabs.NetData m_NetData;
                public required Game.Tools.ControlPoint m_ControlPoint;
                public required Game.Tools.ControlPoint m_BestSnapPosition;
                public required int2 m_LotSize;
                public required Game.Simulation.TerrainHeightData m_TerrainHeightData;

                public required Game.Simulation.WaterSurfaceData<Game.Simulation.SurfaceWater>
                    m_WaterSurfaceData;

                public required ComponentLookup<Game.Net.Node>                m_NodeLookup;
                public required ComponentLookup<Game.Net.Edge>                m_EdgeLookup;
                public required ComponentLookup<Game.Net.Curve>               m_CurveLookup;
                public required BufferLookup<Game.Zones.SubBlock>             m_SubBlockLookup;
                public required ComponentLookup<Game.Net.Composition>         m_CompositionLookup;
                public required ComponentLookup<Game.Prefabs.PrefabRef>       m_PrefabRefLookup;
                public required ComponentLookup<Game.Prefabs.NetData>         m_NetDataLookup;
                public required ComponentLookup<Game.Prefabs.NetGeometryData> m_NetGeometryDataLookup;

                public required ComponentLookup<Game.Prefabs.NetCompositionData>
                    m_NetCompositionDataLookup;

                public required ComponentLookup<Game.Net.EdgeGeometry>      m_EdgeGeoLookup;
                public required ComponentLookup<Game.Net.StartNodeGeometry> m_StartNodeGeoLookup;
                public required ComponentLookup<Game.Net.EndNodeGeometry>   m_EndNodeGeoLookup;
                public required BufferLookup<Game.Net.ConnectedEdge>        m_ConnectedEdgeLookup;
                public required float                                       m_BestDistance;
                public required float                                       m_SnapLevel;

                public bool Intersect(Game.Common.QuadTreeBoundsXZ bounds) {
                    return Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds);
                }

                public void Iterate(Game.Common.QuadTreeBoundsXZ bounds, Entity entity) {
                    if (!Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds))
                        return;

                    if (Colossal.Mathematics.MathUtils.Intersect(bounds.m_Bounds, m_Bounds) &&
                        HandleGeometry(entity)) { }
                }

                private bool HandleGeometry(Entity entity) {
                    var prefabRef    = m_PrefabRefLookup[entity];
                    var controlPoint = m_ControlPoint;
                    controlPoint.m_OriginalEntity = entity;

                    var distance    = m_SnapOffset;
                    var isNode      = m_ConnectedEdgeLookup.HasBuffer(entity);
                    var isCurve     = m_CurveLookup.HasComponent(entity);
                    var isValidRoad = m_SubBlockLookup.HasBuffer(entity);

                    if (!isValidRoad) return false;

                    if (isNode)
                        // dont snap to nodes
                        return false;
                    //var node = m_NodeLookup[entity];
                    //var connectedEdgeBuffer = m_ConnectedEdgeLookup[entity];
                    //for (var i = 0; i < connectedEdgeBuffer.Length; i++) {
                    //    var edge = m_EdgeLookup[connectedEdgeBuffer[i].m_Edge];
                    //    if (edge.m_Start == entity || edge.m_End == entity) {
                    //        return false;
                    //    }
                    //}
                    //if (!m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                    //    return !(math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >=
                    // distance) &&
                    //           HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                    //}
                    //var netGeometryData2 = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                    //distance += netGeometryData2.m_DefaultWidth * 0.5f;
                    //return !(math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >=
                    // distance) &&
                    //       HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                    if (!isCurve) return false;

                    var curve = m_CurveLookup[entity];

                    if (m_CompositionLookup.HasComponent(entity)) {
                        var composition        = m_CompositionLookup[entity];
                        var netCompositionData = m_NetCompositionDataLookup[composition.m_Edge];
                        distance += netCompositionData.m_Width * 0.5f;
                    }

                    if (Colossal.Mathematics.MathUtils.Distance(
                                                                curve.m_Bezier.xz,
                                                                m_ControlPoint.m_HitPosition.xz,
                                                                out controlPoint.m_CurvePosition) >=
                        distance)
                        return false;

                    var snapHeight = Colossal.Mathematics.MathUtils
                                             .Position(curve.m_Bezier, controlPoint.m_CurvePosition).y;

                    return HandleGeometry(controlPoint, snapHeight, prefabRef, false);
                }

                public bool HandleGeometry(Game.Tools.ControlPoint controlPoint,
                                           float                   snapHeight,
                                           Game.Prefabs.PrefabRef  prefabRef,
                                           bool                    ignoreHeightDistance) {
                    if (!m_NetDataLookup.HasComponent(prefabRef.m_Prefab)) return false;

                    var netData = m_NetDataLookup[prefabRef.m_Prefab];

                    var   snapAdded     = false;
                    var   flag2         = true;
                    var   allowEdgeSnap = true;
                    float height;

                    if (m_Elevation < 0f)
                        height = Game.Simulation.TerrainUtils.SampleHeight(ref m_TerrainHeightData,
                                                                           controlPoint.m_HitPosition) +
                                 m_Elevation;
                    else
                        height =
                            Game.Simulation.WaterUtils.SampleHeight(ref m_WaterSurfaceData,
                                                                    ref m_TerrainHeightData,
                                                                    controlPoint.m_HitPosition) +
                            m_Elevation;

                    if (m_NetGeometryDataLookup.HasComponent(prefabRef.m_Prefab)) {
                        var netGeometryData = m_NetGeometryDataLookup[prefabRef.m_Prefab];
                        var bounds          = new Colossal.Mathematics.Bounds1(height);
                        var bounds2         = netGeometryData.m_DefaultHeightRange + snapHeight;
                        if (!Colossal.Mathematics.MathUtils.Intersect(bounds, bounds2)) {
                            flag2 = false;
                            allowEdgeSnap = (netGeometryData.m_Flags &
                                             Game.Net.GeometryFlags.NoEdgeConnection) == 0;
                        }
                    }

                    if (flag2 && !Game.Net.NetUtils.CanConnect(netData, m_NetData)) return snapAdded;

                    if ((m_NetData.m_ConnectLayers & ~netData.m_RequiredLayers &
                         Game.Net.Layer.LaneEditor) != Game.Net.Layer.None)
                        return snapAdded;

                    var num2 = snapHeight - height;

                    if (!ignoreHeightDistance &&
                        !Colossal.Mathematics.MathUtils.Intersect(m_HeightRange, num2))
                        return snapAdded;

                    if (m_NodeLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        if (m_ConnectedEdgeLookup.HasBuffer(controlPoint.m_OriginalEntity)) {
                            var dynamicBuffer = m_ConnectedEdgeLookup[controlPoint.m_OriginalEntity];
                            if (dynamicBuffer.Length != 0) {
                                for (var i = 0; i < dynamicBuffer.Length; i++) {
                                    var edge  = dynamicBuffer[i].m_Edge;
                                    var edge2 = m_EdgeLookup[edge];
                                    if (!(edge2.m_Start != controlPoint.m_OriginalEntity) ||
                                        !(edge2.m_End   != controlPoint.m_OriginalEntity))
                                        HandleCurve(controlPoint, edge, allowEdgeSnap, ref snapAdded);
                                }

                                return snapAdded;
                            }
                        }

                        var candidate = controlPoint;
                        var node      = m_NodeLookup[controlPoint.m_OriginalEntity];
                        candidate.m_Position  = node.m_Position;
                        candidate.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                        Colossal.Mathematics.MathUtils.TryNormalize(ref candidate.m_Direction);

                        candidate.m_SnapPriority = CalculateSnapPriority(
                                                                         m_SnapLevel,
                                                                         1f,
                                                                         1f,
                                                                         m_ControlPoint.m_HitPosition,
                                                                         controlPoint.m_Position,
                                                                         controlPoint.m_Direction
                                                                        );

                        Game.Tools.ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);

                        snapAdded = true;
                    } else if (m_CurveLookup.HasComponent(controlPoint.m_OriginalEntity)) {
                        HandleCurve(controlPoint,
                                    controlPoint.m_OriginalEntity,
                                    allowEdgeSnap,
                                    ref snapAdded);
                    }

                    return snapAdded;
                }

                private void HandleCurve(Game.Tools.ControlPoint controlPoint,
                                         Entity                  curveEntity,
                                         bool                    allowEdgeSnap,
                                         ref bool                snapAdded) {
                    // When you're here, it means we found a valid road edge.
                    var edgeGeo      = m_EdgeGeoLookup[curveEntity];
                    var startNodeGeo = m_StartNodeGeoLookup[curveEntity];
                    var endNodeGeo   = m_EndNodeGeoLookup[curveEntity];
                    var edge         = m_EdgeLookup[curveEntity];

                    var startIsConnected = m_ConnectedEdgeLookup[edge.m_Start].Length > 1;
                    var endIsConnected   = m_ConnectedEdgeLookup[edge.m_End].Length   > 1;

                    var curvesList   = new NativeList<Colossal.Mathematics.Bezier4x3>(Allocator.Temp);
                    var curvesFilter = new NativeList<bool>(Allocator.Temp);

                    curvesList.Add(edgeGeo.m_Start.m_Left);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_Start.m_Right);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_End.m_Left);
                    curvesFilter.Add(true);
                    curvesList.Add(edgeGeo.m_End.m_Right);
                    curvesFilter.Add(true);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Left);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Left.m_Right);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Left.m_Length.y > 1 &&
                                     !startIsConnected);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Left);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Right.m_Length.x > 1 &&
                                     !startIsConnected);
                    curvesList.Add(startNodeGeo.m_Geometry.m_Right.m_Right);
                    curvesFilter.Add(startNodeGeo.m_Geometry.m_Right.m_Length.y > 1);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Left);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Left.m_Length.x > 1);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Left.m_Right);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Left.m_Length.y > 1 && !endIsConnected);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Left);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Right.m_Length.x > 1 && !endIsConnected);
                    curvesList.Add(endNodeGeo.m_Geometry.m_Right.m_Right);
                    curvesFilter.Add(endNodeGeo.m_Geometry.m_Right.m_Length.y > 1);

                    // Find curve closes to our control point.
                    var closestCurve = default(Colossal.Mathematics.Bezier4x3);
                    var closestPoint = default(float);

                    for (var i = 0; i < curvesList.Length; i++) {
                        var curve  = curvesList[i];
                        var filter = curvesFilter[i];

                        if (!filter) continue;

                        // Calculate the distance from the control point to the curve
                        var distance =
                            Colossal.Mathematics.MathUtils.Distance(curve.xz,
                                                                    controlPoint.m_HitPosition.xz,
                                                                    out var t);

                        if (!(distance < m_BestDistance)) continue;

                        // If this curve is closer, update m_BestSnapPoint
                        m_BestDistance = distance;
                        closestCurve   = curve;
                        closestPoint   = t;
                    }

                    if (closestCurve.Equals(default)) {
                        curvesList.Dispose();
                        curvesFilter.Dispose();
                        return;
                    }

                    // Determine what direction we need to rotate
                    var useRight = closestCurve.Equals(edgeGeo.m_Start.m_Left)                 ||
                                   closestCurve.Equals(edgeGeo.m_End.m_Left)                   ||
                                   closestCurve.Equals(startNodeGeo.m_Geometry.m_Left.m_Left)  ||
                                   closestCurve.Equals(startNodeGeo.m_Geometry.m_Right.m_Left) ||
                                   closestCurve.Equals(endNodeGeo.m_Geometry.m_Left.m_Left)    ||
                                   closestCurve.Equals(endNodeGeo.m_Geometry.m_Right.m_Left);

                    var tangent = Colossal.Mathematics.MathUtils.Tangent(closestCurve, closestPoint);

                    m_BestSnapPosition.m_Direction = useRight ?
                        Colossal.Mathematics.MathUtils.Right(tangent.xz) :
                        Colossal.Mathematics.MathUtils.Left(tangent.xz);

                    Colossal.Mathematics.MathUtils.TryNormalize(ref m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_Rotation =
                        Game.Tools.ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_Position =
                        Colossal.Mathematics.MathUtils.Position(closestCurve, closestPoint);

                    // Shift back to center on the curve
                    m_BestSnapPosition.m_Position.xz -=
                        m_BestSnapPosition.m_Direction * m_LotSize.y * 4f;

                    // Calculate and set snap priority
                    m_BestSnapPosition.m_SnapPriority = CalculateSnapPriority(
                                                                              m_SnapLevel,
                                                                              1f,
                                                                              1f,
                                                                              m_ControlPoint
                                                                                  .m_HitPosition,
                                                                              m_BestSnapPosition
                                                                                  .m_Position,
                                                                              m_BestSnapPosition
                                                                                  .m_Direction
                                                                             );

                    //m_BestSnapPosition.m_OriginalEntity = curveEntity;
                    snapAdded = true;

                    curvesList.Dispose();
                    curvesFilter.Dispose();
                }
            }

            private static float2 CalculateSnapPriority(float  level,
                                                        float  basePriority,
                                                        float  heightWeight,
                                                        float3 hitPosition,
                                                        float3 snapPosition,
                                                        float2 snapDirection) {
                var offset = math.abs(snapPosition - hitPosition) / 8f;
                offset *= offset;

                var horizontal = math.min(1f, offset.x + offset.z);
                var diagonal   = math.max(offset.x, offset.z) + math.min(offset.x, offset.z) * 0.001f;

                var priority = basePriority * (2f - horizontal - diagonal) /
                               (1f + offset.y * heightWeight);

                return new float2(level, priority);
            }
        }
    }
}