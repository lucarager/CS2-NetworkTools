// <copyright file="NT_RenderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Rendering;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Color = UnityEngine.Color;

    #endregion

    /// <summary>
    ///     Overlay Rendering System.
    /// </summary>
    public partial class NT_RenderSystem : GameSystemBase {
        private EntityQuery m_EdgeQuery;
        private PrefixedLogger m_Log;
        private EntityQuery m_NodeQuery;
        private EntityQuery m_MarkerQuery;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private PreCullingSystem m_PreCullingSystem;
        private ToolSystem m_ToolSystem;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(NT_RenderSystem));
            m_Log.Debug("OnCreate()");

            m_NodeQuery = SystemAPI.QueryBuilder()
                .WithAll<Node>()
                .WithAny<NT_Highlighted, NT_Selected, NT_Eligible, NT_SelectedFirst, NT_SelectedLast>()
                .WithNone<Deleted, Hidden>()
                .Build();

            m_EdgeQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge>()
                .WithAny<NT_Highlighted, NT_Selected>()
                .WithNone<Deleted, Hidden>()
                .Build();

            m_MarkerQuery = SystemAPI.QueryBuilder()
                .WithAll<NT_Marker>()
                .WithNone<Deleted>()
                .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            var nodeBufferJobHandle = default(JobHandle);
            var markersBufferJobHandle = default(JobHandle);
            var edgeBufferJobHandle = default(JobHandle);
            var lastJobHandle = Dependency;

            if (tool.ShowNodes) {
                // Draw nodes
                var drawNodesJob = new DrawNodesJob {
                    m_Buffer                           = m_OverlayRenderSystem.GetBuffer(out nodeBufferJobHandle),
                    m_EntityTypeHandle                 = SystemAPI.GetEntityTypeHandle(),
                    m_HighlightedComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EligibleComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                    m_SelectedFirstComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_SelectedFirst>(),
                    m_SelectedLastComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NT_SelectedLast>(),
                    m_NodeComponentTypeHandle          = SystemAPI.GetComponentTypeHandle<Node>(),
                    m_NodeGeometryComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NodeGeometry>(),
                    m_ConnectedEdgeComponentTypeHandle = SystemAPI.GetBufferTypeHandle<ConnectedEdge>(true),
                    m_EdgeLookup                       = SystemAPI.GetComponentLookup<Edge>(true),
                    m_EdgeGeometryLookup               = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                    m_CurveLookup                      = SystemAPI.GetComponentLookup<Curve>(true)
                };

                var drawNodesJobHandle = drawNodesJob.ScheduleByRef(m_NodeQuery,
                    JobHandle.CombineDependencies(Dependency,
                        nodeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawNodesJobHandle);
                lastJobHandle = drawNodesJobHandle;
            }

            if (tool.ShowNodes) {
                // Draw markers
                var drawMarkersJob = new DrawMarkersJob {
                    m_Buffer                              = m_OverlayRenderSystem.GetBuffer(out markersBufferJobHandle),
                    m_EntityTypeHandle                    = SystemAPI.GetEntityTypeHandle(),
                    m_NTMarkerPositionComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_MarkerPosition>(),
                    m_HighlightedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                };

                var drawMarkersJobHandle = drawMarkersJob.ScheduleByRef(m_MarkerQuery,
                    JobHandle.CombineDependencies(Dependency,
                        markersBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawMarkersJobHandle);
                lastJobHandle = drawMarkersJobHandle;
            }

            if (tool.ShowEdges) {
                // Draw edges
                var drawEdgesJob = new DrawEdgesJob {
                    m_Buffer                               = m_OverlayRenderSystem.GetBuffer(out edgeBufferJobHandle),
                    m_HighlightedComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle          = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EdgeComponentTypeHandle              = SystemAPI.GetComponentTypeHandle<Edge>(),
                    m_CurveComponentTypeHandle             = SystemAPI.GetComponentTypeHandle<Curve>(),
                    m_EdgeGeometryComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                    m_StartNodeGeometryComponentTypeHandle = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>(),
                    m_EndNodeGeometryComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>(),
                    m_NodeLookup                           = SystemAPI.GetComponentLookup<Node>(true)
                };

                var drawEdgesJobHandle = drawEdgesJob.ScheduleByRef(m_EdgeQuery,
                    JobHandle.CombineDependencies(lastJobHandle,
                        edgeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawEdgesJobHandle);
                lastJobHandle = drawEdgesJobHandle;
            }

            Dependency = lastJobHandle;
        }


        /// <summary>
        ///     Job to draw node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawNodesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected> m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Eligible> m_EligibleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedFirst> m_SelectedFirstComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedLast> m_SelectedLastComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node> m_NodeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NodeGeometry> m_NodeGeometryComponentTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ConnectedEdge> m_ConnectedEdgeComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Edge> m_EdgeLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry> m_EdgeGeometryLookup;
            [ReadOnly] public required ComponentLookup<Curve> m_CurveLookup;
            [ReadOnly] public required EntityTypeHandle m_EntityTypeHandle;

            public static float GetEdgeWidth(Entity nodeEntity, Edge edge, EdgeGeometry geometry) {
                if (edge.m_Start == nodeEntity) {
                    return math.distance(geometry.m_Start.m_Left.a, geometry.m_Start.m_Right.a);
                }
                return math.distance(geometry.m_End.m_Left.a, geometry.m_End.m_Right.a);
            }

            public static float3 GetConnectedEdgeNodePos(Entity node, Edge edge, Curve curve) {
                return edge.m_Start == node ? curve.m_Bezier.a : curve.m_Bezier.d;
            }

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var entitiesArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);
                var nodeGeometriesArray = chunk.GetNativeArray(ref m_NodeGeometryComponentTypeHandle);
                var connectedEdgesArray = chunk.GetBufferAccessor(ref m_ConnectedEdgeComponentTypeHandle);

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity = entitiesArray[i];
                    var node = nodesArray[i];

                    // Check component flags
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);
                    var isEligible = chunk.Has(ref m_EligibleComponentTypeHandle);
                    var isSelectedFirst = chunk.Has(ref m_SelectedFirstComponentTypeHandle);
                    var isSelectedLast = chunk.Has(ref m_SelectedLastComponentTypeHandle);

                    // Determine visual style based on node state
                    Color fillColor;
                    Color borderColor;
                    float diameter;
                    float borderWidth;

                    var connectedEdges = connectedEdgesArray[i];
                    var edgeNodePositions = float3.zero;
                    var diameterSum = 0f;
                    var edgeNodesCount = 0;

                    for (var j = 0; j < connectedEdges.Length; j++) {
                        var edgeEntity = connectedEdges[j];
                        var edge = m_EdgeLookup[edgeEntity.m_Edge];
                        var edgeGeometry = m_EdgeGeometryLookup[edgeEntity.m_Edge];
                        var curve = m_CurveLookup[edgeEntity.m_Edge];

                        // Update max edge diameter
                        diameterSum += GetEdgeWidth(entity, edge, edgeGeometry);

                        //m_Buffer.DrawCircle(Color.red, edgeGeometry.m_Start.m_Left.a,  2f);
                        //m_Buffer.DrawCircle(Color.blue, edgeGeometry.m_Start.m_Right.a, 2f);
                        //m_Buffer.DrawCircle(Color.yellow, edgeGeometry.m_End.m_Left.a,  2f);
                        //m_Buffer.DrawCircle(Color.green, edgeGeometry.m_End.m_Right.a, 2f);

                        // Store connected edge node position 
                        edgeNodePositions += GetConnectedEdgeNodePos(entity, edge, curve);
                        edgeNodesCount++;
                    }

                    // Average the positions of connected edge nodes to get the position for the node overlay,
                    // so that it is centered in the middle of connected edges
                    var averagedPosition = edgeNodePositions / edgeNodesCount;
                    var averagedSize = diameterSum / edgeNodesCount;

                    // Select
                    var position = averagedPosition;
                    var nodeDiameter = averagedSize;
                    var nodeBorderWidth = math.min(1f, averagedSize);

                    // Lift node up slightly so it shows over other elements
                    position.y += 1f;

                    if (isSelectedFirst || isSelectedLast) {
                        // First or last path node
                        fillColor = new Color(0.58f, 0.27f, 1f, 1f);
                        borderColor = new Color(0.58f, 0.27f, 1f, 1f);
                        diameter = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    } else if (isSelected) {
                        //Intermediate path nodes
                        fillColor   = new Color(1f,    1f,    1f, 1f);
                        borderColor = new Color(1f,    1f,    1f, 1f);
                        diameter = 2f;
                        borderWidth = 0.1f;
                    } else if (isHighlighted) {
                        // Hovered eligible node or path nodes
                        fillColor = new Color(1f, 1f, 1f, 1f);
                        borderColor = new Color(1f, 1f, 1f, 1f);
                        diameter = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    } else if (isEligible) {
                        // Eligible but not hovered
                        fillColor = new Color(1f, 1f, 1f, 0.2f);
                        borderColor = new Color(1f, 1f, 1f, 0.6f);
                        diameter = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    } else {
                        // Not eligible - don't render
                        continue;
                    }

                    m_Buffer.DrawCircle(borderColor,
                        fillColor,
                        borderWidth,
                        0,
                        new float2(0, 1),
                        position,
                        diameter);
                }
            }
        }

        /// <summary>
        ///     Job to draw node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawMarkersJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_MarkerPosition> m_NTMarkerPositionComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required EntityTypeHandle m_EntityTypeHandle;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var entitiesArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var positionsArray = chunk.GetNativeArray(ref m_NTMarkerPositionComponentTypeHandle);

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity = entitiesArray[i];
                    var position = positionsArray[i];

                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);

                    var fillColor = isHighlighted ? Color.red : Color.green; // new Color(0.58f, 0.27f, 1f, 1f);
                    var borderColor = isHighlighted ? Color.red : Color.green; // new Color(0.58f, 0.27f, 1f, 1f);
                    var diameter = 3f;
                    var borderWidth = 1f;


                    m_Buffer.DrawCircle(borderColor,
                        fillColor,
                        borderWidth,
                        0,
                        new float2(0, 1),
                        position.Position,
                        diameter);
                }
            }
        }

        /// <summary>
        ///     Job to draw edge overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawEdgesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected> m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge> m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve> m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Node> m_NodeLookup;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var edgesArray = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var edgeGeometriesArray = chunk.GetNativeArray(ref m_EdgeGeometryComponentTypeHandle);
                var startNodeGeometriesArray = chunk.GetNativeArray(ref m_StartNodeGeometryComponentTypeHandle);
                var endNodeGeometriesArray = chunk.GetNativeArray(ref m_EndNodeGeometryComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge = edgesArray[i];
                    var curve = curvesArray[i];
                    var edgeGeometry = edgeGeometriesArray[i];
                    var startNodeGeometry = startNodeGeometriesArray[i];
                    var endNodeGeometry = endNodeGeometriesArray[i];

                    // Check component flags
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);

                    // Determine visual style based on edge state
                    Color color;
                    float width;

                    if (isSelected) {
                        // Selected edge - primary purple/bright
                        color = new Color(0.58f, 0.27f, 1f, 1f);
                        width = 2f;
                    }
                    else if (isHighlighted) {
                        // Hovered/highlighted edge - primary purple/subtle
                        color = new Color(0.58f, 0.27f, 1f, 1f);
                        width = 2f;
                    }
                    else {
                        // Not highlighted or selected - don't render
                        continue;
                    }

                    // Draw the curve bezier
                    m_Buffer.DrawCurve(color, curve.m_Bezier, width);

                    // Draw all curves in the EdgeGeometry
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_Start.m_Left, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_Start.m_Right, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_End.m_Left, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_End.m_Right, width);

                    //m_Buffer.DrawCurve(Color.cyan, startNodeGeometry.m_Geometry.m_Middle, width);
                    //m_Buffer.DrawCurve(Color.red, startNodeGeometry.m_Geometry.m_Left.m_Left, width);
                    //m_Buffer.DrawCurve(Color.green, startNodeGeometry.m_Geometry.m_Left.m_Right, width);
                    //m_Buffer.DrawCurve(Color.white, startNodeGeometry.m_Geometry.m_Right.m_Left, width);
                    //m_Buffer.DrawCurve(Color.black, startNodeGeometry.m_Geometry.m_Right.m_Right, width);

                    //m_Buffer.DrawCurve(Color.gray, endNodeGeometry.m_Geometry.m_Middle, width);
                    //m_Buffer.DrawCurve(Color.blue, endNodeGeometry.m_Geometry.m_Left.m_Left, width);
                    //m_Buffer.DrawCurve(Color.yellow, endNodeGeometry.m_Geometry.m_Left.m_Right, width);
                    //m_Buffer.DrawCurve(Color.magenta, endNodeGeometry.m_Geometry.m_Right.m_Left, width);
                    //m_Buffer.DrawCurve(Color.gray, endNodeGeometry.m_Geometry.m_Right.m_Right, width);
                }
            }
        }
    }
}