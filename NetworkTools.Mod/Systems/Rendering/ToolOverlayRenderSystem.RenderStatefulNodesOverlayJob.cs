namespace NetworkTools.Systems {
    using LucaModsCommon.Rendering;
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Sequential job that renders nodes with state-dependent visuals
        ///     (eligible, highlighted, selected). Shared across tools that need
        ///     general stateful node rendering. Visibility is pre-determined by
        ///     <see cref="FrustumCullEntitiesJob"/>.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct RenderStatefulNodesOverlayJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node>           m_NodeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Eligible>    m_EligibleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>      m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedFirst> m_SelectedFirstComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedLast>  m_SelectedLastComponentTypeHandle;
            [ReadOnly] public required BufferTypeHandle<ConnectedEdge>     m_ConnectedEdgeBufferTypeHandle;
            [ReadOnly] public required ComponentLookup<Edge>               m_EdgeLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>       m_EdgeGeometryLookup;
            [ReadOnly] public required ComponentLookup<StartNodeGeometry>  m_StartNodeGeometryLookup;
            [ReadOnly] public required ComponentLookup<EndNodeGeometry>    m_EndNodeGeometryLookup;
            [ReadOnly] public required NativeParallelHashSet<Entity>       m_VisibleEntities;

            public required CustomOverlayRenderSystem.Buffer m_Buffer;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var entities      = chunk.GetNativeArray(m_EntityTypeHandle);
                var nodesArray    = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);
                var edgesAccessor = chunk.GetBufferAccessor(ref m_ConnectedEdgeBufferTypeHandle);

                var hasSelectedFirst = chunk.Has(ref m_SelectedFirstComponentTypeHandle);
                var hasSelectedLast  = chunk.Has(ref m_SelectedLastComponentTypeHandle);
                var hasSelected      = chunk.Has(ref m_SelectedComponentTypeHandle);
                var hasHighlighted   = chunk.Has(ref m_HighlightedComponentTypeHandle);

                var selectedArray = hasSelected
                    ? chunk.GetNativeArray(ref m_SelectedComponentTypeHandle)
                    : default;

                for (var i = 0; i < entities.Length; i++) {
                    if (!m_VisibleEntities.Contains(entities[i])) {
                        continue;
                    }

                    var entity         = entities[i];
                    var connectedEdges = edgesAccessor[i];

                    if (connectedEdges.Length == 0 || !m_EdgeLookup.HasComponent(connectedEdges[0].m_Edge)) {
                        continue;
                    }

                    // Resolve position from edge geometry
                    var firstEdge = m_EdgeLookup[connectedEdges[0].m_Edge];
                    var position = firstEdge.m_Start == entity
                        ? m_StartNodeGeometryLookup[connectedEdges[0].m_Edge].m_Geometry.m_Middle.d
                        : m_EndNodeGeometryLookup[connectedEdges[0].m_Edge].m_Geometry.m_Middle.d;

                    // Resolve state: selectedFirst/Last > selected > highlighted > eligible
                    ColorPair colorPair;
                    float sizeScale;
                    float borderWidth;

                    if (hasSelectedFirst || hasSelectedLast) {
                        colorPair   = NT_Colors.NodeSelected.Active;
                        sizeScale   = 0.5f;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_ACTIVE;
                    } else if (hasSelected && (selectedArray[i].NodeMode & NodeRenderMode.RenderSelected) != 0) {
                        colorPair   = NT_Colors.NodeSelected.Active;
                        sizeScale   = 0.5f;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_ACTIVE;
                    } else if (hasHighlighted) {
                        colorPair   = NT_Colors.NodeEligible.Hover;
                        sizeScale   = 1f;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_HOVER;
                    } else {
                        colorPair   = NT_Colors.NodeEligible.Rest;
                        sizeScale   = 1f;
                        borderWidth = NT_Dimensions.NODE_BORDER_WIDTH_REST;
                    }

                    var diameter = ComputeNodeDiameter(entity, connectedEdges);
                    if (diameter <= 0f) {
                        continue;
                    }

                    m_Buffer.DrawCircle(colorPair.Border, colorPair.Fill, borderWidth, 0,
                        new float2(0, 1), position, diameter * sizeScale);
                }
            }

            private float ComputeNodeDiameter(Entity entity, DynamicBuffer<ConnectedEdge> connectedEdges) {
                var widthSum = 0f;
                var count    = 0;

                for (var i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;
                    if (!m_EdgeGeometryLookup.HasComponent(edgeEntity)) {
                        continue;
                    }

                    var edge     = m_EdgeLookup[edgeEntity];
                    var geometry = m_EdgeGeometryLookup[edgeEntity];
                    var segment  = edge.m_Start == entity ? geometry.m_Start : geometry.m_End;

                    widthSum += math.distance(segment.m_Left.a, segment.m_Right.a);
                    count++;
                }

                return count > 0 ? widthSum / count * 0.5f : 0f;
            }
        }
    }
}
