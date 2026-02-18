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
        protected struct DrawHandlesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_HandlePosition> m_NTHandlePositionComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleLink> m_NTHandleLinkComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Handle> m_NTHandleComponentTypeHandle;
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
                var positionsArray = chunk.GetNativeArray(ref m_NTHandlePositionComponentTypeHandle);
                var markerLinkArray = chunk.GetNativeArray(ref m_NTHandleLinkComponentTypeHandle);
                var markerArray = chunk.GetNativeArray(ref m_NTHandleComponentTypeHandle);

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity = entitiesArray[i];
                    var position = positionsArray[i];
                    var link = markerLinkArray[i];
                    var marker = markerArray[i];

                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);

                    if ((marker.TypeFlags & HandleTypeFlags.BezierPoint) != HandleTypeFlags.None) {
                        var node = m_NodeLookup[link.LinkedEntity];
                        var curve = m_CurveLookup[link.LinkedEdge];
                        var isControlPoint = (marker.TypeFlags & HandleTypeFlags.BezierControlPoint) != HandleTypeFlags.None;

                        RenderBezierPoint(entity, curve, node, position, marker, link, isControlPoint, isHighlighted, isSelected, m_Buffer);
                    }
                }
            }

            public static void RenderBezierPoint(Entity entity, Curve curve, Node node, NT_HandlePosition position, NT_Handle handle, NT_HandleLink link, bool isControlPoint,
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