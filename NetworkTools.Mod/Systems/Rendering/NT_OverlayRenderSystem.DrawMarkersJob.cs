namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
    using NetworkTools.Components;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.InputSystem;

    public partial class NT_OverlayRenderSystem {
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
            [ReadOnly] public required ComponentTypeHandle<NT_MarkerLink> m_NTMarkerLinkComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Marker> m_NTMarkerComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected> m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Node> m_NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> m_CurveLookup;
            [ReadOnly] public required EntityTypeHandle m_EntityTypeHandle;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var entitiesArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var positionsArray = chunk.GetNativeArray(ref m_NTMarkerPositionComponentTypeHandle);
                var markerLinkArray = chunk.GetNativeArray(ref m_NTMarkerLinkComponentTypeHandle);
                var markerArray = chunk.GetNativeArray(ref m_NTMarkerComponentTypeHandle);

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity = entitiesArray[i];
                    var position = positionsArray[i];
                    var link = markerLinkArray[i];
                    var marker = markerArray[i];

                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);

                    if ((marker.TypeFlags & MarkerTypeFlags.BezierPoint) != MarkerTypeFlags.None) {
                        var node = m_NodeLookup[link.LinkedEntity];
                        var curve = m_CurveLookup[link.LinkedEdge];
                        var isControlPoint = (marker.TypeFlags & MarkerTypeFlags.BezierControlPoint) != MarkerTypeFlags.None;

                        RenderBezierPoint(entity, curve, node, position, marker, link, isControlPoint, isHighlighted, isSelected, m_Buffer);
                    }
                }
            }

            public static void RenderBezierPoint(Entity entity, Curve curve, Node node, NT_MarkerPosition position, NT_Marker marker, NT_MarkerLink link, bool isControlPoint,
                bool isHighlighted, bool isSelected, OverlayRenderSystem.Buffer buffer) {

                Color color;

                if (isSelected) {
                    // Selected edge - primary purple/bright
                    color = Color.red;
                } else if (isHighlighted) {
                    // Hovered/highlighted edge - primary purple/subtle
                    color = Color.green;
                } else {
                    // Not highlighted or selected - don't render
                    color = Color.blue;
                }

                var fillColor = color;
                var borderColor = color;
                var diameter = 3f;
                var borderWidth = 1f;


                buffer.DrawCircle(borderColor,
                    fillColor,
                    borderWidth,
                    0,
                    new float2(0, 1),
                    position.Position,
                    diameter);

                // If this is a control point, also draw a line to the main point
                if (isControlPoint) {
                    var otherPoint = link.Key == 1 ? curve.m_Bezier.a : curve.m_Bezier.d;

                    buffer.DrawDashedLine(color,
                        new Line3.Segment(position.Position, otherPoint),
                        1f,
                        2f,
                        2f);
                }
            }
        }
    }
}