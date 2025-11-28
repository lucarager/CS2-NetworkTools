// <copyright file="NT_AddNodeToolSystem.Jobs.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    public partial class NT_AddNodeToolSystem {
        //public struct CreateDefinitionsJob : IJob {
        //    [ReadOnly] public required TerrainHeightData m_TerrainHeightLookup;
        //    [ReadOnly] public required WaterSurfaceData<SurfaceData>  m_WaterSurfaceLookup;

        //    public void Execute() { }
        //}

        //private struct SnapJob : IJob {
        //    public                     ControlPoint                             ControlPoint => m_ControlPoint;
        //    [ReadOnly] public required NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetTree;
        //    [ReadOnly] public required TerrainHeightData                        m_TerrainHeightLookup;
        //    [ReadOnly] public required WaterSurfaceData<SurfaceData>            m_WaterSurfaceLookup;
        //    [ReadOnly] public required ControlPoint                             m_ControlPoint;
        //    [ReadOnly] public required EntityTypeHandle                         m_EntityTypeHandle;
        //    [ReadOnly] public required ComponentLookup<Node>                    m_NodeLookup;
        //    [ReadOnly] public required ComponentLookup<Edge>                    m_EdgeLookup;
        //    [ReadOnly] public required ComponentLookup<Curve>                   m_CurveLookup;
        //    [ReadOnly] public required ComponentLookup<Composition>             m_CompositionLookup;
        //    [ReadOnly] public required ComponentLookup<PrefabRef>               m_PrefabRefLookup;
        //    [ReadOnly] public required ComponentLookup<NetData>                 m_NetLookupLookup;
        //    [ReadOnly] public required ComponentLookup<NetGeometryData>         m_NetGeometryLookupLookup;
        //    [ReadOnly] public required ComponentLookup<NetCompositionData>      m_NetCompositionLookupLookup;
        //    [ReadOnly] public required BufferLookup<ConnectedEdge>              m_ConnectedEdgeLookup;

        //    public void Execute() {
        //        var searchRadius = 16f;
        //        var bounds = new Bounds3(
        //            m_ControlPoint.m_Position - searchRadius,
        //            m_ControlPoint.m_Position + searchRadius
        //        );
        //        var totalBounds = bounds;
        //        totalBounds.min -= 64f;
        //        totalBounds.max += 64f;
        //        var minDistance      = 16f;
        //        var bestSnapPosition = m_ControlPoint;

        //        //var iterator = new EdgeSnapIterator(
        //        //    totalBounds,
        //        //    bounds,
        //        //    0f,
        //        //    m_ControlPoint.m_Elevation,
        //        //    new Bounds1(
        //        //        -50f,
        //        //        50f),
        //        //    default,
        //        //    m_ControlPoint,
        //        //    bestSnapPosition,
        //        //    m_TerrainHeightLookup,
        //        //    m_WaterSurfaceLookup,
        //        //    m_NodeLookup,
        //        //    m_EdgeLookup,
        //        //    m_CurveLookup,
        //        //    m_CompositionLookup,
        //        //    m_PrefabRefLookup,
        //        //    m_NetLookupLookup,
        //        //    m_NetGeometryLookupLookup,
        //        //    m_NetCompositionLookupLookup,
        //        //    m_ConnectedEdgeLookup,
        //        //    minDistance
        //        ////);

        //        //m_NetTree.Iterate(ref iterator);
        //        //m_ControlPoint = iterator.BestSnapPosition;
        //    }

        //    //public struct EdgeSnapIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
        //    //    public ControlPoint BestSnapPosition => m_BestSnapPosition;

        //    //    private Bounds3                             m_TotalBounds;
        //    //    private Bounds3                             m_Bounds;
        //    //    private float                               m_SnapOffset;
        //    //    private float                               m_Elevation;
        //    //    private Bounds1                             m_HeightRange;
        //    //    private NetData                             m_NetLookup;
        //    //    private ControlPoint                        m_ControlPoint;
        //    //    private ControlPoint                        m_BestSnapPosition;
        //    //    private TerrainHeightData                   m_TerrainHeightLookup;
        //    //    private WaterSurfaceData<SurfaceData>       m_WaterSurfaceLookup;
        //    //    private ComponentLookup<Node>               m_NodeLookup;
        //    //    private ComponentLookup<Edge>               m_EdgeLookup;
        //    //    private ComponentLookup<Curve>              m_CurveLookup;
        //    //    private ComponentLookup<Composition>        m_CompositionLookup;
        //    //    private ComponentLookup<PrefabRef>          m_PrefabRefLookup;
        //    //    private ComponentLookup<NetData>            m_PrefabNetLookup;
        //    //    private ComponentLookup<NetGeometryData>    m_NetGeometryLookupLookup;
        //    //    private ComponentLookup<NetCompositionData> m_NetCompositionLookupLookup;
        //    //    private BufferLookup<ConnectedEdge>         m_ConnectedEdgeLookup;
        //    //    private float                               m_BestDistance;

        //    //    public bool Intersect(QuadTreeBoundsXZ bounds) {
        //    //        return MathUtils.Intersect(
        //    //            bounds.m_Bounds,
        //    //            m_TotalBounds);
        //    //    }

        //    //    public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
        //    //        if (!MathUtils.Intersect(
        //    //                bounds.m_Bounds,
        //    //                m_TotalBounds)) {
        //    //            return;
        //    //        }

        //    //        if (MathUtils.Intersect(
        //    //                bounds.m_Bounds,
        //    //                m_Bounds) && HandleGeometry(entity)) { }
        //    //    }

        //    //    // Largely left unchanged from vanilla
        //    //    private bool HandleGeometry(Entity entity) {
        //    //        var prefabRef    = m_PrefabRefLookup[entity];
        //    //        var controlPoint = m_ControlPoint;
        //    //        controlPoint.m_OriginalEntity = entity;

        //    //        var distance = m_SnapOffset;
        //    //        var isNode   = m_ConnectedEdgeLookup.HasBuffer(entity);
        //    //        var isCurve  = m_CurveLookup.HasComponent(entity);

        //    //        if (isNode) {
        //    //            var node                = m_NodeLookup[entity];
        //    //            var connectedEdgeBuffer = m_ConnectedEdgeLookup[entity];

        //    //            for (var i = 0; i < connectedEdgeBuffer.Length; i++) {
        //    //                var edge = m_EdgeLookup[connectedEdgeBuffer[i].m_Edge];

        //    //                if (edge.m_Start == entity || edge.m_End == entity) {
        //    //                    return false;
        //    //                }
        //    //            }

        //    //            if (!m_NetGeometryLookupLookup.HasComponent(prefabRef.m_Prefab)) {
        //    //                return !(math.distance(
        //    //                           node.m_Position.xz,
        //    //                           m_ControlPoint.m_HitPosition.xz) >= distance) &&
        //    //                       HandleGeometry(
        //    //                           controlPoint,
        //    //                           node.m_Position.y,
        //    //                           prefabRef,
        //    //                           false);
        //    //            }

        //    //            var netGeometryLookup2 = m_NetGeometryLookupLookup[prefabRef.m_Prefab];
        //    //            distance += netGeometryLookup2.m_DefaultWidth * 0.5f;

        //    //            return !(math.distance(
        //    //                       node.m_Position.xz,
        //    //                       m_ControlPoint.m_HitPosition.xz) >= distance) &&
        //    //                   HandleGeometry(
        //    //                       controlPoint,
        //    //                       node.m_Position.y,
        //    //                       prefabRef,
        //    //                       false);
        //    //        }

        //    //        if (!isCurve) {
        //    //            return false;
        //    //        }

        //    //        var curve = m_CurveLookup[entity];

        //    //        if (m_CompositionLookup.HasComponent(entity)) {
        //    //            var composition        = m_CompositionLookup[entity];
        //    //            var netCompositionLookup = m_NetCompositionLookupLookup[composition.m_Edge];
        //    //            distance += netCompositionLookup.m_Width * 0.5f;
        //    //        }

        //    //        if (MathUtils.Distance(
        //    //                curve.m_Bezier.xz,
        //    //                m_ControlPoint.m_HitPosition.xz,
        //    //                out controlPoint.m_CurvePosition) >=
        //    //            distance) {
        //    //            return false;
        //    //        }

        //    //        var snapHeight = MathUtils.Position(
        //    //            curve.m_Bezier,
        //    //            controlPoint.m_CurvePosition).y;

        //    //        return HandleGeometry(
        //    //            controlPoint,
        //    //            snapHeight,
        //    //            prefabRef,
        //    //            false);
        //    //    }

        //    //    // Largely left unchanged from vanilla
        //    //    public bool HandleGeometry(ControlPoint controlPoint, float snapHeight, PrefabRef prefabRef,
        //    //                               bool         ignoreHeightDistance) {
        //    //        if (!m_PrefabNetLookup.HasComponent(prefabRef.m_Prefab)) {
        //    //            return false;
        //    //        }

        //    //        var netLookup = m_PrefabNetLookup[prefabRef.m_Prefab];

        //    //        var   snapAdded = false;
        //    //        var   flag2     = true;
        //    //        var   flag3     = true;
        //    //        float height;

        //    //        if (m_Elevation < 0f) {
        //    //            height = TerrainUtils.SampleHeight(
        //    //                ref m_TerrainHeightLookup,
        //    //                controlPoint.m_HitPosition) + m_Elevation;
        //    //        } else {
        //    //            height =
        //    //            WaterUtils.SampleHeight(
        //    //                ref m_WaterSurfaceLookup,
        //    //                ref m_TerrainHeightLookup,
        //    //                controlPoint.m_HitPosition) +
        //    //            m_Elevation;
        //    //        }

        //    //        if (m_NetGeometryLookupLookup.HasComponent(prefabRef.m_Prefab)) {
        //    //            var netGeometryLookup = m_NetGeometryLookupLookup[prefabRef.m_Prefab];
        //    //            var bounds          = new Bounds1(height);
        //    //            var bounds2         = netGeometryLookup.m_DefaultHeightRange + snapHeight;
        //    //            if (!MathUtils.Intersect(
        //    //                    bounds,
        //    //                    bounds2)) {
        //    //                flag2 = false;
        //    //                flag3 = (netGeometryLookup.m_Flags & GeometryFlags.NoEdgeConnection) == 0;
        //    //            }
        //    //        }

        //    //        if (flag2 && !NetUtils.CanConnect(
        //    //                netLookup,
        //    //                m_NetLookup)) {
        //    //            return snapAdded;
        //    //        }

        //    //        if ((m_NetLookup.m_ConnectLayers & ~netLookup.m_RequiredLayers & Layer.LaneEditor) != Layer.None) {
        //    //            return snapAdded;
        //    //        }

        //    //        var num2 = snapHeight - height;

        //    //        if (!ignoreHeightDistance && !MathUtils.Intersect(
        //    //                m_HeightRange,
        //    //                num2)) {
        //    //            return snapAdded;
        //    //        }

        //    //        if (m_NodeLookup.HasComponent(controlPoint.m_OriginalEntity)) {
        //    //            if (m_ConnectedEdgeLookup.HasBuffer(controlPoint.m_OriginalEntity)) {
        //    //                var dynamicBuffer = m_ConnectedEdgeLookup[controlPoint.m_OriginalEntity];
        //    //                if (dynamicBuffer.Length != 0) {
        //    //                    for (var i = 0; i < dynamicBuffer.Length; i++) {
        //    //                        var edge  = dynamicBuffer[i].m_Edge;
        //    //                        var edge2 = m_EdgeLookup[edge];
        //    //                        if (!(edge2.m_Start != controlPoint.m_OriginalEntity) ||
        //    //                            !(edge2.m_End   != controlPoint.m_OriginalEntity)) {
        //    //                            HandleCurve(
        //    //                                controlPoint,
        //    //                                edge);
        //    //                        }
        //    //                    }

        //    //                    return snapAdded;
        //    //                }
        //    //            }

        //    //            var controlPoint2 = controlPoint;
        //    //            var node          = m_NodeLookup[controlPoint.m_OriginalEntity];
        //    //            controlPoint2.m_Position = node.m_Position;
        //    //            controlPoint2.m_Direction = math.mul(
        //    //                node.m_Rotation,
        //    //                new float3(
        //    //                    0f,
        //    //                    0f,
        //    //                    1f)).xz;
        //    //            MathUtils.TryNormalize(ref controlPoint2.m_Direction);
        //    //            var num3 = 1f;

        //    //            controlPoint2.m_SnapPriority = ToolUtils.CalculateSnapPriority(
        //    //                num3,
        //    //                1f,
        //    //                1f,
        //    //                controlPoint.m_HitPosition,
        //    //                controlPoint2.m_Position,
        //    //                controlPoint2.m_Direction);
        //    //            ToolUtils.AddSnapPosition(
        //    //                ref m_BestSnapPosition,
        //    //                controlPoint2);
        //    //            snapAdded = true;
        //    //        } else if (m_CurveLookup.HasComponent(controlPoint.m_OriginalEntity)) {
        //    //            HandleCurve(
        //    //                controlPoint,
        //    //                controlPoint.m_OriginalEntity);
        //    //        }

        //    //        return snapAdded;
        //    //    }

        //    //    private void HandleCurve(ControlPoint controlPoint, Entity curveEntity) {
        //    //        var curve = m_CurveLookup[curveEntity].m_Bezier;

        //    //        // Find curve closes to our control point.
        //    //        var curveIndex = -1;

        //    //        // Calculate the distance from the control point to the curve
        //    //        var distance = MathUtils.Distance(
        //    //            curve.xz,
        //    //            controlPoint.m_HitPosition.xz,
        //    //            out var t);

        //    //        if (!(distance < m_BestDistance)) {
        //    //            return;
        //    //        }

        //    //        // If this curve is closer, update m_BestSnapPoint
        //    //        m_BestDistance = distance;
        //    //        var tangent = MathUtils.Tangent(
        //    //            curve,
        //    //            t);
        //    //        m_BestSnapPosition.m_Direction = MathUtils.Right(tangent.xz);
        //    //        MathUtils.TryNormalize(ref m_BestSnapPosition.m_Direction);
        //    //        m_BestSnapPosition.m_Rotation = ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);
        //    //        m_BestSnapPosition.m_Position = MathUtils.Position(
        //    //            curve,
        //    //            t);
        //    //    }
        //    //}
        //}
    }
}