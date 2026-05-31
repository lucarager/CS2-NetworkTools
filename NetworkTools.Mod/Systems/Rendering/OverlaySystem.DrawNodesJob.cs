namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using Game.Rendering;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlaySystem {
        /// <summary>
        ///     Job to draw node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawNodesJob : IJobChunk {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required RenderColors m_Colors;
            [ReadOnly] public required RenderDimensions m_Dimensions;
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
            [ReadOnly] public required ComponentLookup<StartNodeGeometry> m_StartNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry> m_EndNodeGeometryLookup;
            [ReadOnly] public required EntityTypeHandle m_EntityTypeHandle;

            public static float GetEdgeWidth(Entity nodeEntity, Edge edge, EdgeGeometry geometry) {
                if (edge.m_Start == nodeEntity) {
                    return math.distance(geometry.m_Start.m_Left.a, geometry.m_Start.m_Right.a);
                }

                return math.distance(geometry.m_End.m_Left.a, geometry.m_End.m_Right.a);
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
                var ntSelectedArray = chunk.GetNativeArray(ref m_SelectedComponentTypeHandle);

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity = entitiesArray[i];
                    var node = nodesArray[i];

                    // Check component flags
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);
                    var isEligible = chunk.Has(ref m_EligibleComponentTypeHandle);
                    var isSelectedFirst = chunk.Has(ref m_SelectedFirstComponentTypeHandle);
                    var isSelectedLast = chunk.Has(ref m_SelectedLastComponentTypeHandle);

                    // Get specific render mode
                    var ntSelected = isSelected ? ntSelectedArray[i] : default;
                    var renderMode = ntSelected.NodeMode;

                    var connectedEdges = connectedEdgesArray[i];

                    if (connectedEdges.Length == 0 || !m_EdgeLookup.HasComponent(connectedEdges[0].m_Edge))
                        continue;

                    var firstEdge = m_EdgeLookup[connectedEdges[0].m_Edge];
                    var position = firstEdge.m_Start == entity
                        ? m_StartNodeGeometryLookup[connectedEdges[0].m_Edge].m_Geometry.m_Middle.d
                        : m_EndNodeGeometryLookup[connectedEdges[0].m_Edge].m_Geometry.m_Middle.d;

                    var diameterSum = 0f;
                    var edgeNodesCount = 0;

                    for (var j = 0; j < connectedEdges.Length; j++) {
                        var edgeEntity = connectedEdges[j];
                        if (!m_EdgeGeometryLookup.HasComponent(edgeEntity.m_Edge))
                            continue;

                        var edge = m_EdgeLookup[edgeEntity.m_Edge];
                        var edgeGeometry = m_EdgeGeometryLookup[edgeEntity.m_Edge];

                        diameterSum += GetEdgeWidth(entity, edge, edgeGeometry);
                        edgeNodesCount++;
                    }

                    if (edgeNodesCount == 0)
                        continue;

                    var averagedSize = diameterSum / edgeNodesCount;
                    var nodeDiameter = averagedSize * 0.5f;
                    ColorPair colorPair;
                    float diameter;
                    float borderWidth;

                    if (isSelectedFirst || isSelectedLast) {
                        colorPair   = NT_Colors.NodeSelected.Active;
                        diameter    = nodeDiameter / 2;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_ACTIVE;
                    } else if (isSelected && (renderMode & NodeRenderMode.RenderSelected) != NodeRenderMode.None) {
                        colorPair   = NT_Colors.NodeSelected.Active;
                        diameter    = nodeDiameter / 2;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_ACTIVE;
                    } else if (isHighlighted) {
                        colorPair   = NT_Colors.NodeEligible.Hover;
                        diameter    = nodeDiameter;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_HOVER;
                    } else if (isEligible) {
                        colorPair   = NT_Colors.NodeEligible.Rest;
                        diameter    = nodeDiameter;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_REST;
                    } else {
                        continue;
                    }

                    m_Buffer.DrawCircle(colorPair.Border,
                        colorPair.Fill,
                        borderWidth,
                        0,
                        new float2(0, 1),
                        position,
                        diameter);
                }
            }
        }
    }
}