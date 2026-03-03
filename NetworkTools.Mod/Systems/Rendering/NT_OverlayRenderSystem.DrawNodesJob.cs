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

    public partial class NT_OverlayRenderSystem {
        /// <summary>
        ///     Job to draw node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawNodesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required RenderColors m_Colors;
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
                    var nodeDiameter = averagedSize * 0.5f;
                    var nodeBorderWidth = math.min(1f, averagedSize);
                    
                    if (isSelectedFirst || isSelectedLast) {
                        // First or last path node
                        fillColor   = (Color)(Vector4)m_Colors.NodeSelectedFirstFill;
                        borderColor = (Color)(Vector4)m_Colors.NodeSelectedFirstBorder;
                        diameter    = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    }
                    else if (isSelected) {
                        // Intermediate path nodes - don't render
                        continue;
                    }
                    else if (isHighlighted) {
                        // Hovered eligible node or path nodes
                        fillColor   = (Color)(Vector4)m_Colors.NodeHighlightedFill;
                        borderColor = (Color)(Vector4)m_Colors.NodeHighlightedBorder;
                        diameter    = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    }
                    else if (isEligible) {
                        // Eligible but not hovered
                        fillColor   = (Color)(Vector4)m_Colors.NodeEligibleFill;
                        borderColor = (Color)(Vector4)m_Colors.NodeEligibleBorder;
                        diameter    = nodeDiameter;
                        borderWidth = nodeBorderWidth;
                    }
                    else {
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
    }
}