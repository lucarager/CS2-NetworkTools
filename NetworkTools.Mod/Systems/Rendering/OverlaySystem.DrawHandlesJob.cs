namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlaySystem {
        /// <summary>
        ///     Job to draw handle overlays.
        ///     Supports point, line, circle, and rotation handle types.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawHandlesJob : IJobChunk {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer          m_Buffer;
            [ReadOnly] public required RenderColors                              m_Colors;
            [ReadOnly] public required RenderDimensions                          m_Dimensions;
            [ReadOnly] public required ComponentTypeHandle<NT_HandlePosition>    m_NTHandlePositionComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleLink>        m_NTHandleLinkComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Handle>            m_NTHandleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted>       m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>          m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleLine>        m_HandleLineComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleCircle>      m_HandleCircleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleRotation>    m_HandleRotationComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleConstraints> m_HandleConstraintsComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_HandleParent>      m_HandleParentComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<NT_HandlePosition>        m_HandlePositionLookup;
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
                var hasRotationComponent    = chunk.Has(ref m_HandleRotationComponentTypeHandle);
                var hasConstraintsComponent = chunk.Has(ref m_HandleConstraintsComponentTypeHandle);
                var hasParentComponent      = chunk.Has(ref m_HandleParentComponentTypeHandle);

                NativeArray<NT_HandleLine>        lineArray        = default;
                NativeArray<NT_HandleCircle>      circleArray      = default;
                NativeArray<NT_HandleRotation>    rotationArray    = default;
                NativeArray<NT_HandleConstraints> constraintsArray = default;
                NativeArray<NT_HandleParent>      parentArray      = default;

                if (hasLineComponent) {
                    lineArray = chunk.GetNativeArray(ref m_HandleLineComponentTypeHandle);
                }

                if (hasCircleComponent) {
                    circleArray = chunk.GetNativeArray(ref m_HandleCircleComponentTypeHandle);
                }

                if (hasRotationComponent) {
                    rotationArray = chunk.GetNativeArray(ref m_HandleRotationComponentTypeHandle);
                }

                if (hasConstraintsComponent) {
                    constraintsArray = chunk.GetNativeArray(ref m_HandleConstraintsComponentTypeHandle);
                }

                if (hasParentComponent) {
                    parentArray = chunk.GetNativeArray(ref m_HandleParentComponentTypeHandle);
                }

                for (var i = 0; i < entitiesArray.Length; i++) {
                    var entity   = entitiesArray[i];
                    var position = positionsArray[i];
                    var link     = markerLinkArray[i];
                    var handle   = markerArray[i];

                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected    = chunk.Has(ref m_SelectedComponentTypeHandle);

                    if (handle.HasAnyFlag(HandleTypeFlags.Line) && hasLineComponent) {
                        var line = lineArray[i];
                        RenderLineHandle(line, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    } else if (handle.HasAnyFlag(HandleTypeFlags.Circle) && hasCircleComponent) {
                        var circle = circleArray[i];
                        RenderCircleHandle(position, circle, handle, isHighlighted, isSelected, m_Buffer);
                    } else if (handle.HasAnyFlag(HandleTypeFlags.Rotation) && hasRotationComponent) {
                        var rotation = rotationArray[i];
                        RenderRotationHandle(position, rotation, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    } else if ((handle.HasAnyFlag(HandleTypeFlags.AxisHandle) || handle.HasAnyFlag(HandleTypeFlags.BezierControlPoint)) && hasConstraintsComponent) {
                        var constraints = constraintsArray[i];
                        RenderAxisHandle(position, constraints, handle, isHighlighted, isSelected, m_Buffer);
                    } else {
                        RenderPointHandle(position, handle, isHighlighted, isSelected, m_Colors, m_Buffer);
                    }

                    // Draw dashed line from child to parent handle (axis handles draw their own connecting line)
                    var isAxisRendered = (handle.HasAnyFlag(HandleTypeFlags.AxisHandle) || handle.HasAnyFlag(HandleTypeFlags.BezierControlPoint)) && hasConstraintsComponent;
                    if (hasParentComponent && !isAxisRendered) {
                        var parentEntity = parentArray[i].Parent;
                        if (parentEntity != Entity.Null && m_HandlePositionLookup.HasComponent(parentEntity)) {
                            var parentPos = m_HandlePositionLookup[parentEntity].Position;
                            var lineColor = (Vector4)m_Colors.HandleSecondaryLineRest;
                            m_Buffer.DrawDashedLine(lineColor,
                                                    new Line3.Segment(position.Position, parentPos),
                                                    0.3f,
                                                    2f,
                                                    2f);
                        }
                    }
                }
            }

            /// <summary>
            ///     Renders a simple point handle.
            /// </summary>
            private static void RenderPointHandle(NT_HandlePosition                position,      NT_Handle handle,
                                                  bool                             isHighlighted, bool      isSelected,
                                                  RenderColors                     colors,
                                                  CustomOverlayRenderSystem.Buffer buffer) {
                GetHandleColors(isHighlighted, isSelected, colors, out var fillColor, out var outlineColor);
                buffer.DrawCircle(outlineColor,
                                  fillColor,
                                  0.5f,
                                  0,
                                  new float2(0, 1),
                                  position.Position,
                                  handle.Size * 2f);
            }

            /// <summary>
            ///     Renders an axis handle: origin dot, connecting line, and draggable circle.
            /// </summary>
            private static void RenderAxisHandle(NT_HandlePosition position, NT_HandleConstraints constraints,
                                                 NT_Handle handle, bool isHighlighted, bool isSelected,
                                                 CustomOverlayRenderSystem.Buffer buffer) {
                var originColorPair = NT_Colors.HandleAxisOrigin.Get(isHighlighted, isSelected);
                var originRadius    = NT_Dimensions.HANDLE_AXIS_ORIGIN_RADIUS;
                buffer.DrawCircle(originColorPair.Fill,
                                  constraints.Origin,
                                  originRadius * 2f);

                var circleColorPair = NT_Colors.HandleAxisCircle.Get(isHighlighted, isSelected);
                var circleRadius    = handle.Size;
                buffer.DrawCircle(circleColorPair.Border,
                                  circleColorPair.Fill,
                                  NT_Dimensions.HANDLE_AXIS_CIRCLE_OUTLINE,
                                  0,
                                  new float2(0, 1),
                                  position.Position,
                                  circleRadius * 2f);

                var lineColorPair  = NT_Colors.HandleAxisLine.Get(isHighlighted, isSelected);
                var originToHandle = position.Position - constraints.Origin;
                var dist           = math.length(originToHandle);

                if (dist > originRadius + circleRadius) {
                    var dir = originToHandle / dist;
                    buffer.DrawLine(lineColorPair.Fill,
                                          new Line3.Segment(constraints.Origin + dir * originRadius,
                                                            position.Position  - dir * circleRadius),
                                          NT_Dimensions.HANDLE_AXIS_LINE_WIDTH, true);
                }
            }

            /// <summary>
            ///     Renders a line handle.
            /// </summary>
            private static void RenderLineHandle(NT_HandleLine                    line,          NT_Handle handle,
                                                 bool                             isHighlighted, bool      isSelected,
                                                 RenderColors                     colors,
                                                 CustomOverlayRenderSystem.Buffer buffer) {
                //GetHandleColors(isHighlighted, isSelected, colors, out var fillColor, out var outlineColor);
                //var width = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 1.5f : 1f;

                //buffer.DrawLine(color, new Line3.Segment(line.PointA, line.PointB), width);

                //// Draw small circles at endpoints
                //buffer.DrawCircle(color, color, 0.3f, 0, new float2(0, 1), line.PointA, 1.5f);
                //buffer.DrawCircle(color, color, 0.3f, 0, new float2(0, 1), line.PointB, 1.5f);
            }

            /// <summary>
            ///     Renders a circle handle.
            /// </summary>
            private static void RenderCircleHandle(NT_HandlePosition                position, NT_HandleCircle circle,
                                                   NT_Handle                        handle,
                                                   bool                             isHighlighted, bool isSelected,
                                                   CustomOverlayRenderSystem.Buffer buffer) {
                var colors      = NT_Colors.HandleCircle.Get(isHighlighted, isSelected);
                var borderWidth = NT_Dimensions.HANDLE_CIRCLE_OUTLINE_WIDTH;

                // Draw the circle outline
                buffer.DrawCircle(colors.Border,
                                  colors.Fill,
                                  borderWidth,
                                  0,
                                  new float2(0, 1),
                                  position.Position,
                                  circle.Radius * 2f);

                // Draw a small center point
                buffer.DrawCircle(colors.Fill, colors.Fill, 0.3f, 0, new float2(0, 1), position.Position, 1f);
            }

            /// <summary>
            ///     Renders a rotation handle: circle outline with an angle indicator line.
            /// </summary>
            private static void RenderRotationHandle(NT_HandlePosition                position,
                                                     NT_HandleRotation                rotation,      NT_Handle handle,
                                                     bool                             isHighlighted, bool isSelected,
                                                     RenderColors                     colors,
                                                     CustomOverlayRenderSystem.Buffer buffer) {
                GetHandleColors(isHighlighted, isSelected, colors, out var fillColor, out var outlineColor);
                var borderWidth = handle.HasAnyFlag(HandleTypeFlags.Primary) ? 1f : 0.5f;
                var center      = position.Position;
                var direction   = rotation.GetDirection();
                var anglePoint  = center + direction * rotation.Radius;

                // Draw the circle outline
                buffer.DrawCircle(outlineColor,
                                  new Color(255, 255, 255, 0),
                                  borderWidth,
                                  0,
                                  new float2(0, 1),
                                  position.Position,
                                  rotation.Radius * 2f);

                // Draw a thin line from center to angle point
                buffer.DrawLine(outlineColor, new Line3.Segment(center, anglePoint), 0.5f);

                // Draw a small handle dot at the angle point
                buffer.DrawCircle(outlineColor, outlineColor, 0.2f, 0, new float2(0, 1), anglePoint, 1f);
            }

            private static void GetCircleHandleColors(bool      isHighlighted, bool      isSelected,
                                                      out Color fillColor,     out Color outlineColor) {
                var pair = NT_Colors.HandleCircle.Get(isHighlighted, isSelected);
                fillColor    = pair.Fill;
                outlineColor = pair.Border;
            }

            /// <summary>
            ///     Gets the appropriate color for a handle based on its state.
            /// </summary>
            private static void GetHandleColors(bool        isHighlighted, bool        isSelected, RenderColors colors,
                                                out Vector4 fillColor,     out Vector4 outlineColor) {
                if (isSelected) {
                    fillColor    = colors.HandleFillSelected;
                    outlineColor = colors.HandleOutlineSelected;
                } else if (isHighlighted) {
                    fillColor    = colors.HandleFillHover;
                    outlineColor = colors.HandleOutlineHover;
                } else {
                    fillColor    = colors.HandleFillRest;
                    outlineColor = colors.HandleOutlineRest;
                }
            }
        }
    }
}