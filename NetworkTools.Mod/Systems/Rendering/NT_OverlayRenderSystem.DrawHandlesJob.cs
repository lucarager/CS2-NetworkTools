namespace NetworkTools.Systems {
    #region Using Statements

    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    #endregion

    public partial class NT_OverlayRenderSystem {
        /// <summary>
        ///     Job to draw handle overlays.
        ///     Supports point, line, and circle handle types.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawHandlesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer                m_Buffer;
            [ReadOnly] public required RenderColors                              m_Colors;
            [ReadOnly] public required ComponentTypeHandle<NT_HandlePosition>    m_NTHandlePositionComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleLink>        m_NTHandleLinkComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Handle>            m_NTHandleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted>       m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>          m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleLine>        m_HandleLineComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleCircle>      m_HandleCircleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleConstraints> m_HandleConstraintsComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Node>                     m_NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>                    m_CurveLookup;
            [ReadOnly] public required EntityTypeHandle                          m_EntityTypeHandle;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                var entitiesArray           = chunk.GetNativeArray(m_EntityTypeHandle);
                var positionsArray          = chunk.GetNativeArray(ref m_NTHandlePositionComponentTypeHandle);
                var markerLinkArray         = chunk.GetNativeArray(ref m_NTHandleLinkComponentTypeHandle);
                var markerArray             = chunk.GetNativeArray(ref m_NTHandleComponentTypeHandle);
                var hasLineComponent        = chunk.Has(ref m_HandleLineComponentTypeHandle);
                var hasCircleComponent      = chunk.Has(ref m_HandleCircleComponentTypeHandle);
                var hasConstraintsComponent = chunk.Has(ref m_HandleConstraintsComponentTypeHandle);

                NativeArray<NT_HandleLine>        lineArray        = default;
                NativeArray<NT_HandleCircle>      circleArray      = default;
                NativeArray<NT_HandleConstraints> constraintsArray = default;

                if (hasLineComponent) {
                    lineArray = chunk.GetNativeArray(ref m_HandleLineComponentTypeHandle);
                }

                if (hasCircleComponent) {
                    circleArray = chunk.GetNativeArray(ref m_HandleCircleComponentTypeHandle);
                }

                if (hasConstraintsComponent) {
                    constraintsArray = chunk.GetNativeArray(ref m_HandleConstraintsComponentTypeHandle);
                }

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity   = entitiesArray[i];
                    var position = positionsArray[i];
                    var link     = markerLinkArray[i];
                    var handle   = markerArray[i];

                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected    = chunk.Has(ref m_SelectedComponentTypeHandle);

                    // Determine handle type and render accordingly
                    if (handle.HasAnyFlag(HandleTypeFlags.Line) && hasLineComponent) {
                        var line = lineArray[i];
                        RenderLineHandle(line, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    } else if (handle.HasAnyFlag(HandleTypeFlags.Circle) && hasCircleComponent) {
                        var circle = circleArray[i];
                        RenderCircleHandle(circle, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    } else if (handle.HasAnyFlag(HandleTypeFlags.ParameterRange) && hasConstraintsComponent) {
                        // Parameter handle with range indicator (origin dot + line to handle)
                        var constraints = constraintsArray[i];
                        RenderParameterRangeHandle(position, constraints, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    } else if (handle.HasAnyFlag(HandleTypeFlags.BezierPoint)) {
                        // Bezier point handles
                        if (m_NodeLookup.HasComponent(link.LinkedEntity) &&
                            m_CurveLookup.HasComponent(link.LinkedEdge)) {
                            var node           = m_NodeLookup[link.LinkedEntity];
                            var curve          = m_CurveLookup[link.LinkedEdge];
                            var isControlPoint = handle.HasAnyFlag(HandleTypeFlags.BezierControlPoint);
                            RenderBezierPointHandle(curve,
                                                    position,
                                                    handle,
                                                    link,
                                                    isControlPoint,
                                                    isHighlighted,
                                                    isSelected,
                                                    m_Colors,
                                                    m_Buffer);
                        }
                    } else {
                        // Default point handle
                        RenderPointHandle(position, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    }
                }
            }

            /// <summary>
            ///     Renders a simple point handle.
            /// </summary>
            private static void RenderPointHandle(NT_HandlePosition          position,      NT_Handle handle,
                                                  bool                       isHighlighted, bool      isSelected,
                                                  RenderColors               colors,
                                                  OverlayRenderSystem.Buffer buffer) {
                var color    = GetHandleColor(handle, isHighlighted, isSelected, colors);
                var diameter = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 4f : 2.5f;

                buffer.DrawCircle(color, color, 0.5f, 0, new float2(0, 1), position.Position, diameter);
            }

            /// <summary>
            ///     Renders a parameter range handle with origin dot and line from origin to handle.
            /// </summary>
            private static void RenderParameterRangeHandle(NT_HandlePosition position, NT_HandleConstraints constraints,
                                                           NT_Handle handle, bool isHighlighted, bool isSelected,
                                                           RenderColors colors,
                                                           OverlayRenderSystem.Buffer buffer) {
                var color       = GetHandleColor(handle, isHighlighted, isSelected, colors);
                var originColor = (Color)(Vector4)colors.HandleOrigin;
                var diameter    = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 4f : 2.5f;

                // Draw origin dot (small circle at the start of valid range)
                buffer.DrawCircle(originColor, originColor, 0.3f, 0, new float2(0, 1), constraints.Origin, 1f);

                // Draw solid line from origin to handle position
                buffer.DrawDashedLine(originColor, new Line3.Segment(constraints.Origin, position.Position), 0.5f, 2f, 2f);

                // Draw the handle itself (larger circle at current position)
                buffer.DrawCircle(color, originColor, 0.5f, 0, new float2(0, 1), position.Position, diameter);
            }

            /// <summary>
            ///     Renders a bezier point handle with connection line for control points.
            /// </summary>
            private static void RenderBezierPointHandle(Curve curve, NT_HandlePosition position, NT_Handle handle,
                                                        NT_HandleLink link, bool isControlPoint, bool isHighlighted,
                                                        bool isSelected,
                                                        RenderColors colors,
                                                        OverlayRenderSystem.Buffer buffer) {
                var color = GetHandleColor(handle, isHighlighted, isSelected, colors);
                var diameter = handle.HasAnyFlag(HandleTypeFlags.Primary)   ? 4f :
                               handle.HasAnyFlag(HandleTypeFlags.Secondary) ? 2.5f : 3f;

                buffer.DrawCircle(color, color, 0.5f, 0, new float2(0, 1), position.Position, diameter);

                // If this is a control point, also draw a line to the main point
                if (isControlPoint) {
                    var otherPoint = link.Key == 1 ? curve.m_Bezier.a : curve.m_Bezier.d;
                    buffer.DrawDashedLine(color, new Line3.Segment(position.Position, otherPoint), 0.5f, 2f, 2f);
                }
            }

            /// <summary>
            ///     Renders a line handle.
            /// </summary>
            private static void RenderLineHandle(NT_HandleLine              line,          NT_Handle handle,
                                                 bool                       isHighlighted, bool      isSelected,
                                                 RenderColors               colors,
                                                 OverlayRenderSystem.Buffer buffer) {
                var color = GetHandleColor(handle, isHighlighted, isSelected, colors);
                var width = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 1.5f : 1f;

                buffer.DrawLine(color, new Line3.Segment(line.PointA, line.PointB), width);

                // Draw small circles at endpoints
                buffer.DrawCircle(color, color, 0.3f, 0, new float2(0, 1), line.PointA, 1.5f);
                buffer.DrawCircle(color, color, 0.3f, 0, new float2(0, 1), line.PointB, 1.5f);
            }

            /// <summary>
            ///     Renders a circle handle.
            /// </summary>
            private static void RenderCircleHandle(NT_HandleCircle            circle,        NT_Handle handle,
                                                   bool                       isHighlighted, bool      isSelected,
                                                   RenderColors               colors,
                                                   OverlayRenderSystem.Buffer buffer) {
                var color       = GetHandleColor(handle, isHighlighted, isSelected, colors);
                var borderWidth = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 1f : 0.5f;

                // Draw the circle outline
                buffer.DrawCircle(default, color, borderWidth, 0, new float2(0, 1), circle.Center, circle.Radius * 2f);

                // Draw a small center point
                buffer.DrawCircle(color, color, 0.3f, 0, new float2(0, 1), circle.Center, 1f);
            }

            /// <summary>
            ///     Gets the appropriate color for a handle based on its state.
            /// </summary>
            private static Color GetHandleColor(NT_Handle handle, bool isHighlighted, bool isSelected, RenderColors colors) {
                if (isSelected) {
                    return (Color)(Vector4)colors.HandleSelected;
                }

                if (isHighlighted) {
                    return (Color)(Vector4)colors.HandleHighlighted;
                }

                // Default colors based on handle purpose
                if (handle.HasAnyFlag(HandleTypeFlags.SlopeControl)) {
                    return (Color)(Vector4)colors.HandleSlopeControl;
                }

                if (handle.HasAnyFlag(HandleTypeFlags.ShapeControl)) {
                    return (Color)(Vector4)colors.HandleShapeControl;
                }

                return (Color)(Vector4)colors.HandleDefault;
            }
        }
    }
}